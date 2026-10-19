<#
.SYNOPSIS
  Sets up the lab server (Windows 11 Pro + an existing SQL Server) for Diagnostic Labs. Safe to run again.

.DESCRIPTION
  Run from an ELEVATED PowerShell, logged in as the Windows administrator who installed SQL Server
  (that account must be a SQL sysadmin). Start with -CheckOnly: it only reports and changes nothing.

  Steps (each can be skipped with its switch):
   1. Pre-flight report: Windows, SQL instance, edition, login mode, TCP port, firewall, network profile, power.
   2. SQL network: TCP/IP on, fixed port 1433, mixed-mode logins, firewall rule for the LAN, no sleep/hibernate.
      (Restarts the SQL Server service if a setting changed.)
   3. Database: creates it when missing; on an EMPTY database runs db\schema.sql then db\seed.sql.
      An existing database is left alone (use -ApplySchemaUpdate to run the idempotent schema.sql on it).
   4. Application login (dl_app): read/write on this database only, random password.
   5. Update share \\SERVER\DiagnosticLabs (current, config), a read-only Windows account for the stations,
      and config\appsettings.Local.json holding the connection string.
   6. Backups: folder permissions for the SQL service, a first verified backup, optional scheduled tasks.

  The db scripts are looked up in .\db next to this script, then in ..\..\db (the repo). Use Make-ServerKit.ps1 to
  build a folder or zip holding both.

.EXAMPLE
  .\Install-Server.ps1 -CheckOnly
  .\Install-Server.ps1 -BackupDir D:\Backups -CopyDir E:\LabBackups -RegisterBackupTasks
#>
[CmdletBinding()]
param(
    # "." or ".\NAME" or "SERVER\NAME". Detected when omitted and there is exactly one instance.
    [string]$SqlServer,
    [string]$Database = 'DiagnosticLabsV2',
    [string]$AppLogin = 'dl_app',
    [int]$Port = 1433,
    [string]$ShareRoot = 'C:\DiagnosticLabs',
    [string]$ShareName = 'DiagnosticLabs',
    [string]$ShareUser = 'dlupdate',
    # What stations put in their connection string: this PC's name by default; use a fixed IP if names do not resolve.
    [string]$ServerAddress = $env:COMPUTERNAME,
    [string]$BackupDir,
    [string]$CopyDir,
    [switch]$RegisterBackupTasks,
    [switch]$CheckOnly,
    [switch]$SkipNetwork,
    [switch]$SkipShare,
    [switch]$SkipBackups,
    [switch]$ApplySchemaUpdate,
    # Create the tables but not the starting data: for the one-time legacy migration, which needs an EMPTY database (db\migrate-from-legacy.sql adds the menu itself).
    [switch]$SkipSeed,
    [switch]$SetPrivateNetwork
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

# ---------- helpers ----------
function Step([string]$t)  { Write-Host "`n=== $t" -ForegroundColor Cyan }
function Ok([string]$t)    { Write-Host "  [ok]   $t" -ForegroundColor Green }
function Info([string]$t)  { Write-Host "  [info] $t" }
function Warn([string]$t)  { Write-Host "  [warn] $t" -ForegroundColor Yellow }
function Fail([string]$t)  { Write-Host "  [FAIL] $t" -ForegroundColor Red }

function Test-Admin {
    $p = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
function Require-Admin([string]$what) {
    if (-not (Test-Admin)) { throw "$what needs an elevated PowerShell (Run as administrator)." }
}

function New-SafePassword([int]$length = 24) {
    $chars = 'abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789-_.!#%*+@'.ToCharArray()
    $bytes = New-Object byte[] $length
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $pw = -join ($bytes | ForEach-Object { $chars[$_ % $chars.Length] })
    # Windows complexity: make sure all classes are present.
    if ($pw -cnotmatch '[a-z]' -or $pw -cnotmatch '[A-Z]' -or $pw -notmatch '\d') { return New-SafePassword $length }
    $pw
}

$script:ConnString = $null
function Invoke-Sql([string]$sql, [string]$db = 'master', [switch]$Scalar) {
    $conn = New-Object System.Data.SqlClient.SqlConnection ($script:ConnString + "Database=$db;")
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 0
        if ($Scalar) { $cmd.ExecuteScalar() } else { [void]$cmd.ExecuteNonQuery() }
    }
    finally { $conn.Dispose() }
}
function Invoke-SqlFile([string]$path, [string]$db, [hashtable]$vars = @{}) {
    # Same batches sqlcmd would send: split on lines holding only GO; $(NAME) variables substituted.
    $text = [System.IO.File]::ReadAllText($path)
    foreach ($k in $vars.Keys) { $text = $text.Replace('$(' + $k + ')', $vars[$k]) }
    $batches = [regex]::Split($text, '(?im)^\s*GO\s*$') | Where-Object { $_.Trim() }
    $n = 0
    foreach ($b in $batches) { Invoke-Sql $b $db; $n++ }
    $n
}

# ---------- locate the SQL instance ----------
Start-Transcript -Path (Join-Path $env:TEMP "install-server-$(Get-Date -Format yyyyMMdd-HHmmss).log") | Out-Null
try {
    Step '1. Pre-flight report'
    $os = Get-CimInstance Win32_OperatingSystem
    Info "Windows: $($os.Caption) build $($os.BuildNumber), computer name $env:COMPUTERNAME"
    if ($os.Caption -notmatch 'Windows 1[01]|Server') { Warn 'Not Windows 10/11/Server: V2 stations and this server are only tested on Windows 10/11.' }
    if (-not (Test-Admin)) { Warn 'Not elevated: the steps that change Windows (network, share, folders) will stop; database steps still run.' }

    $services = @(Get-Service | Where-Object { $_.Name -eq 'MSSQLSERVER' -or $_.Name -like 'MSSQL$*' })
    if (-not $services) { throw 'No SQL Server service found on this PC.' }
    Info ('SQL Server instances: ' + (($services | ForEach-Object { "$($_.Name) ($($_.Status))" }) -join ', '))

    if (-not $SqlServer) {
        if ($services.Count -ne 1) { throw 'More than one SQL instance: pass -SqlServer ".\NAME" (or "." for the default instance).' }
        $SqlServer = if ($services[0].Name -eq 'MSSQLSERVER') { '.' } else { '.\' + $services[0].Name.Substring(6) }
    }
    $instName  = if ($SqlServer -match '\\(.+)$') { $Matches[1] } else { 'MSSQLSERVER' }
    $svcName   = if ($instName -eq 'MSSQLSERVER') { 'MSSQLSERVER' } else { 'MSSQL$' + $instName }
    $script:ConnString = "Server=$SqlServer;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15;"
    Info "Using SQL Server: $SqlServer (service $svcName)"

    $svc = Get-CimInstance Win32_Service -Filter "Name='$svcName'"
    if (-not $svc) { throw "Service $svcName not found." }
    Info "SQL service account: $($svc.StartName); status: $($svc.State); start mode: $($svc.StartMode)"
    if ($svc.StartMode -ne 'Auto') { Warn 'The SQL service does not start automatically.' }

    $info = Invoke-Sql "SELECT CAST(SERVERPROPERTY('Edition') AS nvarchar(100)) + ' | ' + CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(50)) + ' | sysadmin=' + CAST(IS_SRVROLEMEMBER('sysadmin') AS varchar(1)) + ' | integratedOnly=' + CAST(SERVERPROPERTY('IsIntegratedSecurityOnly') AS varchar(1))" -Scalar
    Info "SQL: $info"
    $isSysadmin = $info -match 'sysadmin=1'
    $integratedOnly = $info -match 'integratedOnly=1'
    if (-not $isSysadmin) { throw 'This Windows account is not a SQL sysadmin. Run as the account that installed SQL Server.' }
    if ($integratedOnly) { Warn 'Mixed-mode (SQL login) authentication is OFF: the app login cannot sign in until it is enabled (step 2 does it).' }

    $instKey = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL' -ErrorAction SilentlyContinue).$instName
    $netKey  = if ($instKey) { "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instKey\MSSQLServer" } else { $null }
    $tcpKey  = if ($netKey) { "$netKey\SuperSocketNetLib\Tcp" } else { $null }
    $tcpOn = $false; $ipAllPort = ''; $ipAllDyn = ''
    if ($tcpKey -and (Test-Path $tcpKey)) {
        $tcpOn = [bool](Get-ItemProperty $tcpKey).Enabled
        $ipAll = Get-ItemProperty "$tcpKey\IPAll"
        $ipAllPort = [string]$ipAll.TcpPort; $ipAllDyn = [string]$ipAll.TcpDynamicPorts
    }
    Info "TCP/IP enabled: $tcpOn; static port: '$ipAllPort'; dynamic port: '$ipAllDyn'"

    $rule = Get-NetFirewallRule -DisplayName 'DiagnosticLabs SQL*' -ErrorAction SilentlyContinue
    Info ('Firewall rule for SQL: ' + $(if ($rule) { 'present' } else { 'missing' }))
    $profiles = @(Get-NetConnectionProfile -ErrorAction SilentlyContinue)
    foreach ($p in $profiles) { Info "Network '$($p.Name)': $($p.NetworkCategory)" }
    if ($profiles | Where-Object NetworkCategory -eq 'Public') { Warn 'A network is "Public": Windows blocks incoming connections there. Set it to Private (-SetPrivateNetwork does it).' }
    $standby = (powercfg /query SCHEME_CURRENT SUB_SLEEP STANDBYIDLE 2>$null | Select-String 'Current AC Power Setting Index').ToString().Trim()
    Info "Sleep after (AC): $standby"
    $existing = Invoke-Sql "SELECT CASE WHEN DB_ID(N'$Database') IS NULL THEN 'no' ELSE 'yes' END" -Scalar
    Info "Database $Database exists: $existing"
    if ($existing -eq 'yes') {
        $tables = Invoke-Sql 'SELECT COUNT(*) FROM sys.tables' $Database -Scalar
        Info "  tables in it: $tables"
    }

    if ($CheckOnly) { Write-Host "`nCheck only: nothing was changed." -ForegroundColor Cyan; return }

    # ---------- 2. network ----------
    if (-not $SkipNetwork) {
        Step '2. SQL network, firewall and power'
        Require-Admin 'Changing SQL network settings'
        $restart = $false
        if (-not $tcpKey) { throw 'Cannot find the SQL TCP/IP registry settings for this instance.' }
        if (-not $tcpOn) { Set-ItemProperty $tcpKey -Name Enabled -Value 1; $restart = $true; Ok 'TCP/IP enabled' } else { Ok 'TCP/IP already enabled' }
        if ($ipAllPort -ne "$Port" -or $ipAllDyn -ne '') {
            Set-ItemProperty "$tcpKey\IPAll" -Name TcpPort -Value "$Port"
            Set-ItemProperty "$tcpKey\IPAll" -Name TcpDynamicPorts -Value ''
            $restart = $true; Ok "Fixed port $Port set, dynamic ports off"
        } else { Ok "Port $Port already fixed" }
        if ($integratedOnly) {
            Set-ItemProperty $netKey -Name LoginMode -Value 2
            $restart = $true; Ok 'Mixed-mode authentication enabled'
        }
        if ($restart) {
            Warn 'Restarting the SQL Server service (anyone connected is disconnected)...'
            Restart-Service -Name $svcName -Force
            Start-Sleep -Seconds 5
            Ok 'SQL service restarted'
        }
        if ($svc.StartMode -ne 'Auto') { Set-Service -Name $svcName -StartupType Automatic; Ok 'SQL service set to start automatically' }

        if (-not $rule) {
            New-NetFirewallRule -DisplayName 'DiagnosticLabs SQL TCP' -Direction Inbound -Protocol TCP -LocalPort $Port `
                -Action Allow -Profile Private,Domain -RemoteAddress LocalSubnet | Out-Null
            Ok "Firewall: TCP $Port allowed from the local subnet (Private/Domain networks)"
        } else { Ok 'Firewall rule already there' }
        if ($SetPrivateNetwork) {
            Get-NetConnectionProfile | Where-Object NetworkCategory -eq 'Public' | Set-NetConnectionProfile -NetworkCategory Private
            Ok 'Public networks set to Private'
        }
        powercfg /change standby-timeout-ac 0 | Out-Null
        powercfg /change disk-timeout-ac 0 | Out-Null
        powercfg /hibernate off | Out-Null
        Ok 'Sleep, disk sleep and hibernate off (AC power)'
    }

    # ---------- 3. database ----------
    Step "3. Database $Database"
    $dbRoot = @((Join-Path $PSScriptRoot 'db'), (Join-Path $PSScriptRoot '..\..\db')) | Where-Object { Test-Path (Join-Path $_ 'schema.sql') } | Select-Object -First 1
    if (-not $dbRoot) { throw 'db\schema.sql and db\seed.sql not found (use Make-ServerKit.ps1 or run from the repo).' }
    $dbRoot = (Resolve-Path $dbRoot).Path
    Info "SQL scripts from $dbRoot"

    $created = $false
    if ($existing -eq 'no') { Invoke-Sql "CREATE DATABASE [$Database]"; $created = $true; Ok 'Database created' }
    $tables = [int](Invoke-Sql 'SELECT COUNT(*) FROM sys.tables' $Database -Scalar)
    if ($tables -eq 0 -and $SkipSeed) {
        $n = Invoke-SqlFile (Join-Path $dbRoot 'schema.sql') $Database
        Ok "schema.sql applied ($n batches); seed.sql skipped (-SkipSeed): the database is empty, ready for db\migrate-from-legacy.sql"
    }
    elseif ($tables -eq 0) {
        $adminPw = $env:DL_ADMIN_PASSWORD
        if (-not $adminPw) {
            $s1 = Read-Host 'Temporary password for the Admin user (8+ letters/digits, no quotes; changed at first sign-in)' -AsSecureString
            $adminPw = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s1))
        }
        if ($adminPw.Length -lt 8 -or $adminPw -notmatch '^[A-Za-z0-9._!#%*+@-]+$') { throw 'Admin temporary password: 8+ characters, letters, digits and . _ ! # % * + @ - only.' }
        $n = Invoke-SqlFile (Join-Path $dbRoot 'schema.sql') $Database
        Ok "schema.sql applied ($n batches)"
        [void](Invoke-SqlFile (Join-Path $dbRoot 'seed.sql') $Database @{ DL_ADMIN_PASSWORD = $adminPw })
        Ok 'seed.sql applied (Admin user created with the temporary password)'
        $adminPw = $null
    }
    elseif ($ApplySchemaUpdate) {
        $n = Invoke-SqlFile (Join-Path $dbRoot 'schema.sql') $Database
        Ok "schema.sql re-applied to the existing database ($n batches; it only applies missing migrations)"
    }
    else {
        Info "Database already has $tables tables: left unchanged (-ApplySchemaUpdate runs schema.sql on it)."
    }
    $ver = Invoke-Sql 'SELECT COUNT(*) FROM __EFMigrationsHistory' $Database -Scalar
    Ok "Migrations recorded: $ver"

    # ---------- 4. app login ----------
    Step "4. Application login $AppLogin"
    $loginExists = [int](Invoke-Sql "SELECT COUNT(*) FROM sys.server_principals WHERE name = N'$AppLogin'" -Scalar) -gt 0
    $configDir  = Join-Path $ShareRoot 'config'
    $configFile = Join-Path $configDir 'appsettings.Local.json'
    $writeConfig = $false
    $appPw = $null
    if (-not $loginExists) {
        $appPw = New-SafePassword
        Invoke-Sql "CREATE LOGIN [$AppLogin] WITH PASSWORD = N'$appPw', CHECK_POLICY = ON, DEFAULT_DATABASE = [$Database]"
        $writeConfig = $true; Ok 'Login created with a random password'
    }
    elseif (-not (Test-Path $configFile)) {
        $appPw = New-SafePassword
        Invoke-Sql "ALTER LOGIN [$AppLogin] WITH PASSWORD = N'$appPw'"
        $writeConfig = $true; Ok 'Login existed but no config file: password reset'
    }
    else { Ok 'Login and config already exist: password left as it is' }
    Invoke-Sql "IF USER_ID(N'$AppLogin') IS NULL CREATE USER [$AppLogin] FOR LOGIN [$AppLogin];
                ALTER ROLE db_datareader ADD MEMBER [$AppLogin];
                ALTER ROLE db_datawriter ADD MEMBER [$AppLogin];" $Database
    Ok "$AppLogin can read and write $Database only (no schema changes)"

    # ---------- 5. share ----------
    if (-not $SkipShare) {
        Step "5. Update share \\$env:COMPUTERNAME\$ShareName"
        Require-Admin 'Creating the share and the Windows account'
        New-Item -ItemType Directory -Force -Path (Join-Path $ShareRoot 'current'), $configDir | Out-Null
        $shareSecret = $null
        if (-not (Get-LocalUser -Name $ShareUser -ErrorAction SilentlyContinue)) {
            $shareSecret = New-SafePassword 20
            New-LocalUser -Name $ShareUser -Password (ConvertTo-SecureString $shareSecret -AsPlainText -Force) `
                -PasswordNeverExpires -UserMayNotChangePassword -Description 'Read-only account for station updates' | Out-Null
            Ok "Windows account $ShareUser created"
        } else { Ok "Windows account $ShareUser already exists" }
        if (-not (Get-SmbShare -Name $ShareName -ErrorAction SilentlyContinue)) {
            New-SmbShare -Name $ShareName -Path $ShareRoot -ReadAccess $ShareUser -FullAccess 'BUILTIN\Administrators' | Out-Null
            Ok "Share $ShareName created (stations: read only)"
        } else { Ok "Share $ShareName already exists" }
        icacls $ShareRoot /grant "${ShareUser}:(OI)(CI)RX" | Out-Null
        if ($writeConfig) {
            $json = @{ ConnectionStrings = @{ DiagnosticLabs = "Server=$ServerAddress,$Port;Database=$Database;User Id=$AppLogin;Password=$appPw;TrustServerCertificate=True" } } | ConvertTo-Json
            [System.IO.File]::WriteAllText($configFile, $json, (New-Object System.Text.UTF8Encoding $false))
            Ok "Wrote $configFile"
        }
    }

    # ---------- 6. backups ----------
    if (-not $SkipBackups) {
        Step '6. Backups'
        if (-not $BackupDir) { throw 'Pass -BackupDir (a folder on a different disk than the database is best) or -SkipBackups.' }
        Require-Admin 'Setting folder permissions'
        New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
        icacls $BackupDir /grant "$($svc.StartName):(OI)(CI)M" | Out-Null
        Ok "SQL service account ($($svc.StartName)) may write $BackupDir"
        $backupScript = Join-Path $PSScriptRoot 'Backup-Database.ps1'
        $bargs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $backupScript, '-Server', $SqlServer, '-Database', $Database, '-BackupDir', $BackupDir)
        if ($CopyDir) { $bargs += @('-CopyDir', $CopyDir) }
        & powershell.exe @bargs
        if ($LASTEXITCODE -ne 0) { Fail "First backup failed (see $BackupDir\backup.log)"; } else { Ok 'First backup written and verified' }
        if ($RegisterBackupTasks) {
            $rargs = @{ Server = $SqlServer; BackupDir = $BackupDir }
            if ($CopyDir) { $rargs.CopyDir = $CopyDir }
            & (Join-Path $PSScriptRoot 'Register-BackupTasks.ps1') @rargs
        } else { Info 'Schedule the nightly backups: Register-BackupTasks.ps1 -BackupDir ... (or rerun with -RegisterBackupTasks).' }
    }

    # ---------- summary ----------
    Step 'Done. Keep these for the stations'
    Info "Server name stations use: $ServerAddress,$Port    Database: $Database"
    if ($shareSecret) {
        Write-Host "  Station read account : $env:COMPUTERNAME\$ShareUser" -ForegroundColor Yellow
        Write-Host "  Its password (shown once, store it safely): $shareSecret" -ForegroundColor Yellow
        Info "On each station:  cmdkey /add:$env:COMPUTERNAME /user:$env:COMPUTERNAME\$ShareUser /pass:<that password>"
    }
    Info 'Next: publish the release to the share, then install a station:'
    Info "  On the build PC: .\tools\deploy\Publish-Release.ps1 -Version 1.0.0 -Zip, carry release-1.0.0.zip here, unzip it, robocopy into $ShareRoot\current (/MIR /XF version.txt), then copy version.txt last."
    Info 'Then sign in as Admin with the temporary password and set the service prices and company setup.'
}
finally {
    Stop-Transcript | Out-Null
}
