<#
.SYNOPSIS
  Backs up the DiagnosticLabsV2 database, verifies the file, copies it to a second location and
  removes old backups. Meant to be run by Task Scheduler on the server (SQL Server Express has no Agent).

.DESCRIPTION
  - Full or differential backup (CHECKSUM, compressed when the edition allows it).
  - RESTORE VERIFYONLY on the new file.
  - Optional copy to a second disk / NAS (-CopyDir). A backup on the database's own disk is not a backup.
  - Retention: deletes backups older than -RetentionDays, but always keeps the newest 3 full backups.
  - Writes backup.log next to the backups; exits non-zero on any failure so the task shows as failed.

  Needs no sqlcmd: it talks to SQL Server through .NET, available on every Windows PC.

  IMPORTANT: -BackupDir is written by the SQL Server *service*, not by you. Give the service account
  (NT Service\MSSQL$SQLEXPRESS or NT Service\MSSQLSERVER) Modify rights on that folder.
  The login running the script needs the sysadmin or db_backupoperator role. Run the scheduled task as
  the Windows administrator who installed SQL Server; SYSTEM is NOT a sysadmin on SQL Server 2022.

.EXAMPLE
  .\Backup-Database.ps1 -Server ".\SQLEXPRESS" -BackupDir D:\Backups -CopyDir \\NAS\labs
  .\Backup-Database.ps1 -Server ".\SQLEXPRESS" -BackupDir D:\Backups -Type Differential
#>
[CmdletBinding()]
param(
    [string]$Server = '.\SQLEXPRESS',
    [string]$Database = 'DiagnosticLabsV2',
    [Parameter(Mandatory)][string]$BackupDir,
    [string]$CopyDir,
    [ValidateSet('Full', 'Differential')][string]$Type = 'Full',
    [int]$RetentionDays = 14,
    # Leave empty to use the Windows account running the script (recommended).
    [string]$SqlUser,
    [string]$SqlPassword
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
$logFile = Join-Path $BackupDir 'backup.log'

function Write-Log([string]$msg) {
    $line = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $msg
    Add-Content -Path $logFile -Value $line
    Write-Host $line
}

function Invoke-Sql([string]$sql, [hashtable]$params = @{}) {
    $cs = "Server=$Server;Database=master;TrustServerCertificate=True;Connect Timeout=30;"
    $cs += if ($SqlUser) { "User Id=$SqlUser;Password=$SqlPassword;" } else { 'Integrated Security=True;' }
    $conn = New-Object System.Data.SqlClient.SqlConnection $cs
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $sql
        $cmd.CommandTimeout = 0   # backups of a growing database must not time out
        foreach ($k in $params.Keys) { [void]$cmd.Parameters.AddWithValue($k, $params[$k]) }
        [void]$cmd.ExecuteNonQuery()
    }
    finally { $conn.Dispose() }
}

$failed = $false
try {
    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $kind  = if ($Type -eq 'Full') { 'FULL' } else { 'DIFF' }
    $file  = Join-Path $BackupDir "${Database}_${kind}_${stamp}.bak"
    $db    = '[' + $Database.Replace(']', ']]') + ']'
    $diff  = if ($Type -eq 'Differential') { 'DIFFERENTIAL, ' } else { '' }

    Write-Log "Starting $Type backup of $Database on $Server -> $file"

    try {
        Invoke-Sql "BACKUP DATABASE $db TO DISK = @f WITH ${diff}COMPRESSION, CHECKSUM, INIT, NAME = @n" @{ '@f' = $file; '@n' = "$Database $kind $stamp" }
    }
    catch {
        # Some editions refuse COMPRESSION; a plain backup is better than none.
        if ($_.Exception.Message -match 'COMPRESSION') {
            Write-Log 'Compression not available on this edition, retrying without it.'
            Invoke-Sql "BACKUP DATABASE $db TO DISK = @f WITH ${diff}CHECKSUM, INIT, NAME = @n" @{ '@f' = $file; '@n' = "$Database $kind $stamp" }
        }
        else { throw }
    }

    Invoke-Sql 'RESTORE VERIFYONLY FROM DISK = @f WITH CHECKSUM' @{ '@f' = $file }
    $mb = [math]::Round((Get-Item $file).Length / 1MB, 1)
    Write-Log "Backup written and verified ($mb MB)."

    if ($CopyDir) {
        try {
            New-Item -ItemType Directory -Force -Path $CopyDir | Out-Null
            Copy-Item -LiteralPath $file -Destination $CopyDir -Force
            Write-Log "Copied to $CopyDir"
        }
        catch {
            Write-Log "COPY FAILED to ${CopyDir}: $($_.Exception.Message)"
            $failed = $true   # the primary backup is fine, but the task must still show as failed
        }
    }

    # Retention: age-based, but never drop below the newest 3 full backups.
    foreach ($dir in @($BackupDir, $CopyDir) | Where-Object { $_ -and (Test-Path $_) }) {
        $all   = Get-ChildItem -LiteralPath $dir -Filter "${Database}_*.bak" | Sort-Object LastWriteTime -Descending
        $keep  = @($all | Where-Object Name -like "${Database}_FULL_*" | Select-Object -First 3)
        $limit = (Get-Date).AddDays(-$RetentionDays)
        $old   = $all | Where-Object { $_.LastWriteTime -lt $limit -and $keep.FullName -notcontains $_.FullName }
        foreach ($f in $old) {
            Remove-Item -LiteralPath $f.FullName -Force
            Write-Log "Removed old backup $($f.Name)"
        }
    }
}
catch {
    Write-Log "BACKUP FAILED: $($_.Exception.Message)"
    $failed = $true
}

if ($failed) { exit 1 }
exit 0
