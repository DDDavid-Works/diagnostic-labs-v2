<#
.SYNOPSIS
  Installs a new release on the lab server: backup, database update, then the update share. Run it in an ELEVATED PowerShell.

.DESCRIPTION
  Input is the release zip from Publish-Release.ps1 -Zip (or an unzipped folder). Steps:
   1. Checks the release (version.txt, exe, no appsettings.Local.json, schema.sql present) and the version order.
   2. Backs up the database (Backup-Database.ps1: verified, copied to -CopyDir when given). Stops if that fails.
   3. Runs _server\schema.sql on the database (idempotent: applies only the missing migrations).
   4. Copies the share's current release to "previous", then mirrors the new one into "current".
      version.txt is removed first and written last, so a station launching in between just starts its installed copy.
   5. Checks the result, writes update-state.json and update.log in the share root, removes the staging folder.

  Stations pick the release up on their next launch from the shortcut. A station still on the old program while the
  database is already updated shows "close it and open Diagnostic Labs from its shortcut", which is the intended path.

  Roll back with Rollback-Server.ps1 (it uses "previous" and the backup recorded in update-state.json).

.EXAMPLE
  .\Update-Server.ps1 -Release D:\Drop\release-1.0.1.zip -BackupDir D:\Backups -CopyDir E:\LabBackups
  .\Update-Server.ps1 -Release D:\Drop\release-1.0.1.zip      # reuses the BackupDir of the last update
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Release,
    [string]$SqlServer = '.\SQLEXPRESS',
    [string]$Database = 'DiagnosticLabsV2',
    [string]$ShareName = 'DiagnosticLabs',
    [string]$ShareRoot,
    [string]$BackupDir,
    [string]$CopyDir,
    # Skip steps only when you know why (for example a release that has no database change and a backup minutes ago).
    [switch]$SkipBackup,
    [switch]$SkipSchema,
    # Allow the same or an older version number than the one on the share.
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

function Step([string]$t) { Write-Host "`n=== $t" -ForegroundColor Cyan }
function Ok([string]$t)   { Write-Host "  [ok]   $t" -ForegroundColor Green }
function Info([string]$t) { Write-Host "  [info] $t" }
function Warn([string]$t) { Write-Host "  [warn] $t" -ForegroundColor Yellow }

function Invoke-Sql([string]$sql, [string]$db, [switch]$Scalar) {
    $conn = New-Object System.Data.SqlClient.SqlConnection "Server=$SqlServer;Database=$db;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15;"
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 0
        if ($Scalar) { $cmd.ExecuteScalar() } else { [void]$cmd.ExecuteNonQuery() }
    }
    finally { $conn.Dispose() }
}
function Get-Migrations {
    $conn = New-Object System.Data.SqlClient.SqlConnection "Server=$SqlServer;Database=$Database;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15;"
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand(); $cmd.CommandText = "IF OBJECT_ID(N'__EFMigrationsHistory') IS NOT NULL SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId ELSE SELECT TOP 0 CAST('' AS nvarchar(150))"
        $r = $cmd.ExecuteReader()
        while ($r.Read()) { $r.GetString(0) }   # one string per row; callers wrap the call in @()
        $r.Close()
    }
    finally { $conn.Dispose() }
}
function Read-Version([string]$folder) {
    $f = Join-Path $folder 'version.txt'
    if (Test-Path -LiteralPath $f) { (Get-Content -LiteralPath $f -Raw).Trim() } else { '' }
}
# Mirror a folder; version.txt goes last so nobody sees a half-copied release as complete.
function Copy-Release([string]$from, [string]$to, [string]$version) {
    New-Item -ItemType Directory -Force -Path $to | Out-Null
    Remove-Item (Join-Path $to 'version.txt') -Force -ErrorAction SilentlyContinue
    robocopy $from $to /MIR /XF version.txt /XD _server /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy $from -> $to failed (exit $LASTEXITCODE)." }
    Set-Content -Path (Join-Path $to 'version.txt') -Value $version -Encoding ASCII
}
# "1.0.10" vs "1.0.9" and "2026.10.11.1430": compare numerically when both parse, else as text.
function Compare-Versions([string]$a, [string]$b) {
    try { ([version]$a).CompareTo([version]$b) } catch { [string]::Compare($a, $b, $true) }
}

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this in an elevated PowerShell (Run as administrator).' }

if (-not $ShareRoot) {
    $smb = Get-SmbShare -Name $ShareName -ErrorAction SilentlyContinue
    $ShareRoot = if ($smb) { $smb.Path } else { 'C:\DiagnosticLabs' }
}
$current  = Join-Path $ShareRoot 'current'
$previous = Join-Path $ShareRoot 'previous'
$staging  = Join-Path $ShareRoot '_staging'
$stateFile = Join-Path $ShareRoot 'update-state.json'
if (-not (Test-Path $current)) { throw "No 'current' folder in $ShareRoot. Is this the right share root? (Install-Server.ps1 creates it.)" }

$state = if (Test-Path $stateFile) { Get-Content $stateFile -Raw | ConvertFrom-Json } else { $null }
if (-not $BackupDir -and $state -and $state.BackupDir) { $BackupDir = $state.BackupDir; Info "Using the backup folder of the last update: $BackupDir" }
if (-not $SkipBackup -and -not $BackupDir) { throw 'Pass -BackupDir (or -SkipBackup).' }

Start-Transcript -Path (Join-Path $env:TEMP "update-server-$(Get-Date -Format yyyyMMdd-HHmmss).log") | Out-Null
try {
    # ---------- 1. check the release ----------
    Step '1. Checking the release'
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    if (Test-Path -LiteralPath $Release -PathType Leaf) {
        Info "Unpacking $Release"
        Expand-Archive -LiteralPath $Release -DestinationPath $staging -Force
    }
    elseif (Test-Path -LiteralPath $Release -PathType Container) {
        Info "Copying $Release"
        New-Item -ItemType Directory -Force -Path $staging | Out-Null
        robocopy $Release $staging /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed (exit $LASTEXITCODE)." }
    }
    else { throw "Release not found: $Release" }

    $newVersion = Read-Version $staging
    $oldVersion = Read-Version $current
    if (-not $newVersion) { throw 'The release has no version.txt.' }
    if (-not (Test-Path (Join-Path $staging 'DiagnosticLabs.Wpf.exe'))) { throw 'The release has no DiagnosticLabs.Wpf.exe.' }
    if (Test-Path (Join-Path $staging 'appsettings.Local.json')) { throw 'appsettings.Local.json is in the release; stations keep their own. Rebuild it.' }
    $schemaFile = Join-Path $staging '_server\schema.sql'
    if (-not $SkipSchema -and -not (Test-Path $schemaFile)) { throw 'The release has no _server\schema.sql (build the zip with Publish-Release.ps1 -Zip, or pass -SkipSchema).' }
    Info "Share now: '$oldVersion'   New release: '$newVersion'"
    if ($oldVersion -and (Compare-Versions $newVersion $oldVersion) -le 0 -and -not $Force) {
        throw "Version $newVersion is not newer than $oldVersion. Pass -Force if that is intended."
    }
    Ok 'Release looks complete'

    # ---------- 2. backup ----------
    $backupFile = $null
    if ($SkipBackup) { Warn 'Backup skipped.' }
    else {
        Step '2. Database backup'
        $before = Get-Date
        $bargs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Backup-Database.ps1'),
                   '-Server', $SqlServer, '-Database', $Database, '-BackupDir', $BackupDir)
        if ($CopyDir) { $bargs += @('-CopyDir', $CopyDir) }
        & powershell.exe @bargs
        if ($LASTEXITCODE -ne 0) { throw "Backup failed (see $BackupDir\backup.log). Nothing was changed." }
        $backupFile = Get-ChildItem -LiteralPath $BackupDir -Filter "${Database}_FULL_*.bak" |
            Where-Object LastWriteTime -ge $before.AddSeconds(-5) | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
        if (-not $backupFile) { throw 'The backup ran but its file was not found.' }
        Ok "Backup: $backupFile"
    }

    # ---------- 3. database ----------
    $migBefore = @(Get-Migrations)
    $migAfter = $migBefore
    if ($SkipSchema) { Warn 'Database update skipped.' }
    else {
        Step '3. Database update (schema.sql)'
        $text = [System.IO.File]::ReadAllText($schemaFile)
        $batches = [regex]::Split($text, '(?im)^\s*GO\s*$') | Where-Object { $_.Trim() }
        try { foreach ($b in $batches) { Invoke-Sql $b $Database } }
        catch {
            $hint = if ($backupFile) { "The share was NOT changed. If the database is half-updated, restore the backup by hand (DEPLOYMENT.md, 'Restoring'): $backupFile" } else { 'There is no backup from this run.' }
            throw "schema.sql failed: $($_.Exception.Message)`n$hint"
        }
        $migAfter = @(Get-Migrations)
        $added = @($migAfter | Where-Object { $migBefore -notcontains $_ })
        if ($added) { Ok "Applied $($added.Count) migration(s): $($added -join ', ')" } else { Ok 'Database was already up to date' }
    }

    # ---------- 4. share ----------
    Step '4. Update share'
    if ($oldVersion) {
        Info "Keeping $oldVersion as 'previous'"
        Copy-Release $current $previous $oldVersion
    }
    Copy-Release $staging $current $newVersion
    Ok "current is now $newVersion"

    # ---------- 5. verify and record ----------
    Step '5. Checking the result'
    if ((Read-Version $current) -ne $newVersion) { throw 'current\version.txt does not show the new version.' }
    if (-not (Test-Path (Join-Path $current 'DiagnosticLabs.Wpf.exe'))) { throw 'current has no DiagnosticLabs.Wpf.exe.' }
    robocopy $staging $current /L /E /XF version.txt /XD _server /NJH /NJS /NP /NDL | Out-Null
    if ($LASTEXITCODE -ne 0) { Warn 'current differs from the release; run the update again.' } else { Ok 'current matches the release file for file' }

    $record = [ordered]@{
        Version = $newVersion; PreviousVersion = $oldVersion; UpdatedAt = (Get-Date).ToString('s')
        BackupFile = $backupFile; BackupDir = $BackupDir; CopyDir = $CopyDir
        SqlServer = $SqlServer; Database = $Database
        MigrationsBefore = $migBefore.Count; MigrationsAfter = $migAfter.Count
    }
    $record | ConvertTo-Json | Set-Content -Path $stateFile -Encoding UTF8
    Add-Content -Path (Join-Path $ShareRoot 'update.log') -Value ('{0:yyyy-MM-dd HH:mm:ss}  {1} -> {2}  migrations {3} -> {4}  backup {5}' -f (Get-Date), $oldVersion, $newVersion, $migBefore.Count, $migAfter.Count, $backupFile)
    Remove-Item $staging -Recurse -Force

    Step "Done: $newVersion is live"
    Info 'Stations update on their next launch from the shortcut. Open the app once on this PC to confirm.'
    if ($migAfter.Count -gt $migBefore.Count) { Warn 'The database changed: going back to the old version also needs the database restored (Rollback-Server.ps1 -RestoreDatabase).' }
}
finally {
    Stop-Transcript | Out-Null
}
