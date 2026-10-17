<#
.SYNOPSIS
  Puts the previous release back on the update share, and optionally restores the pre-update database backup.
  Run it in an ELEVATED PowerShell on the server.

.DESCRIPTION
  Reads update-state.json (written by Update-Server.ps1) in the share root.

  App only: copies "previous" over "current". Stations go back to that version on their next launch.
  If the last update applied database migrations, the old program refuses to start against the newer database
  ("close it and open from its shortcut"), so the script stops and asks for -RestoreDatabase.

  -RestoreDatabase: restores the backup taken just before the update. EVERYTHING ENTERED SINCE THAT BACKUP IS LOST.
  It first takes a safety backup of the current database, kicks out all connections, restores, and you must type
  the database name to confirm. Tell the lab to stop working first.

.EXAMPLE
  .\Rollback-Server.ps1                      # app only (refuses if the database has newer migrations)
  .\Rollback-Server.ps1 -RestoreDatabase     # app and database, back to the state before the last update
#>
[CmdletBinding()]
param(
    [string]$ShareName = 'DiagnosticLabs',
    [string]$ShareRoot,
    [switch]$RestoreDatabase,
    # Defaults to the backup recorded by the last update.
    [string]$BackupFile
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

function Step([string]$t) { Write-Host "`n=== $t" -ForegroundColor Cyan }
function Ok([string]$t)   { Write-Host "  [ok]   $t" -ForegroundColor Green }
function Info([string]$t) { Write-Host "  [info] $t" }
function Warn([string]$t) { Write-Host "  [warn] $t" -ForegroundColor Yellow }

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this in an elevated PowerShell (Run as administrator).' }

if (-not $ShareRoot) {
    $smb = Get-SmbShare -Name $ShareName -ErrorAction SilentlyContinue
    $ShareRoot = if ($smb) { $smb.Path } else { 'C:\DiagnosticLabs' }
}
$current  = Join-Path $ShareRoot 'current'
$previous = Join-Path $ShareRoot 'previous'
$stateFile = Join-Path $ShareRoot 'update-state.json'
if (-not (Test-Path $stateFile)) { throw "No update-state.json in ${ShareRoot}: Update-Server.ps1 has not run here, so there is nothing recorded to roll back." }
$state = Get-Content $stateFile -Raw | ConvertFrom-Json
$prevVersionFile = Join-Path $previous 'version.txt'
if (-not (Test-Path $prevVersionFile)) { throw "No 'previous' release in $ShareRoot." }
$prevVersion = (Get-Content $prevVersionFile -Raw).Trim()

$sqlServer = $state.SqlServer; $database = $state.Database
function Invoke-Sql([string]$sql, [string]$db = 'master', [switch]$Scalar) {
    $conn = New-Object System.Data.SqlClient.SqlConnection "Server=$sqlServer;Database=$db;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15;"
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 0
        if ($Scalar) { $cmd.ExecuteScalar() } else { [void]$cmd.ExecuteNonQuery() }
    }
    finally { $conn.Dispose() }
}

Start-Transcript -Path (Join-Path $env:TEMP "rollback-server-$(Get-Date -Format yyyyMMdd-HHmmss).log") | Out-Null
try {
    $liveVersion = (Get-Content (Join-Path $current 'version.txt') -Raw).Trim()
    Step 'Rollback plan'
    Info "Share now: $liveVersion   Going back to: $prevVersion (updated at $($state.UpdatedAt))"
    $dbChanged = [int]$state.MigrationsAfter -gt [int]$state.MigrationsBefore
    Info ("Last update applied {0} migration(s)." -f ([int]$state.MigrationsAfter - [int]$state.MigrationsBefore))

    if ($dbChanged -and -not $RestoreDatabase) {
        throw "The last update changed the database, and version $prevVersion cannot run against it. Rerun with -RestoreDatabase (everything entered since $($state.UpdatedAt) is lost), or fix forward with a new release."
    }

    if ($RestoreDatabase) {
        if (-not $BackupFile) { $BackupFile = $state.BackupFile }
        if (-not $BackupFile -or -not (Test-Path -LiteralPath $BackupFile)) { throw "Backup file not found: '$BackupFile'. Pass -BackupFile." }
        Warn "This restores $database from $BackupFile and LOSES all data entered since $($state.UpdatedAt)."
        Warn 'Everyone must be out of the app.'
        $typed = Read-Host "Type the database name ($database) to continue"
        if ($typed -ne $database) { throw 'Not confirmed. Nothing was changed.' }

        Step 'Safety backup of the current database'
        if (-not $state.BackupDir) { throw 'No BackupDir recorded; cannot take the safety backup.' }
        $bargs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Backup-Database.ps1'),
                   '-Server', $sqlServer, '-Database', $database, '-BackupDir', $state.BackupDir)
        if ($state.CopyDir) { $bargs += @('-CopyDir', $state.CopyDir) }
        & powershell.exe @bargs
        if ($LASTEXITCODE -ne 0) { throw 'The safety backup failed. Nothing was changed.' }
        Ok 'Safety backup written (newest FULL file in the backup folder)'

        Step 'Restoring the database'
        $db = '[' + $database.Replace(']', ']]') + ']'
        $file = $BackupFile.Replace("'", "''")
        Invoke-Sql "ALTER DATABASE $db SET SINGLE_USER WITH ROLLBACK IMMEDIATE"
        try { Invoke-Sql "RESTORE DATABASE $db FROM DISK = N'$file' WITH REPLACE, RECOVERY" }
        finally { Invoke-Sql "ALTER DATABASE $db SET MULTI_USER" }
        $n = Invoke-Sql 'SELECT COUNT(*) FROM __EFMigrationsHistory' $database -Scalar
        Ok "Database restored; migrations recorded: $n"
    }

    Step 'Restoring the previous release on the share'
    Remove-Item (Join-Path $current 'version.txt') -Force -ErrorAction SilentlyContinue
    robocopy $previous $current /MIR /XF version.txt /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed (exit $LASTEXITCODE)." }
    Set-Content -Path (Join-Path $current 'version.txt') -Value $prevVersion -Encoding ASCII
    Ok "current is now $prevVersion"

    # The update is undone: forget it, so a second rollback cannot "undo" it again.
    Move-Item $stateFile (Join-Path $ShareRoot 'update-state.rolled-back.json') -Force
    Add-Content -Path (Join-Path $ShareRoot 'update.log') -Value ('{0:yyyy-MM-dd HH:mm:ss}  ROLLBACK {1} -> {2}  database restored: {3}' -f (Get-Date), $liveVersion, $prevVersion, [bool]$RestoreDatabase)

    Step "Done: $prevVersion is live again"
    Info 'Stations go back to it on their next launch from the shortcut.'
}
finally {
    Stop-Transcript | Out-Null
}
