<#
.SYNOPSIS
  Creates the Task Scheduler jobs on the server: a nightly full backup and optional daytime differentials.
  Run once, from an elevated PowerShell, logged in as the administrator who owns the SQL Server install.

.EXAMPLE
  .\Register-BackupTasks.ps1 -Server ".\SQLEXPRESS" -BackupDir D:\Backups -CopyDir \\NAS\labs `
      -FullAt 22:00 -DifferentialAt 12:00,16:00
#>
[CmdletBinding()]
param(
    [string]$Server = '.\SQLEXPRESS',
    [Parameter(Mandatory)][string]$BackupDir,
    [string]$CopyDir,
    [string]$FullAt = '22:00',
    [string[]]$DifferentialAt = @()
)

$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Backup-Database.ps1'
$extra  = "-Server `"$Server`" -BackupDir `"$BackupDir`""
if ($CopyDir) { $extra += " -CopyDir `"$CopyDir`"" }

function New-BackupTask([string]$name, [string]$at, [string]$type) {
    $args = "-NoProfile -ExecutionPolicy Bypass -File `"$script`" $extra -Type $type"
    $action  = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $args
    $trigger = New-ScheduledTaskTrigger -Daily -At $at
    # Run even if the PC was off at the scheduled time, and never overlap with itself.
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 2)
    # Runs as the current user; Windows asks for their password so the task can run while logged off.
    $cred = Get-Credential -UserName "$env:USERDOMAIN\$env:USERNAME" -Message "Password for the account that runs '$name'"
    Register-ScheduledTask -TaskName $name -Action $action -Trigger $trigger -Settings $settings `
        -User $cred.UserName -Password $cred.GetNetworkCredential().Password -RunLevel Highest -Force | Out-Null
    Write-Host "Registered '$name' ($type at $at)"
}

New-BackupTask 'DiagnosticLabs Backup (full)' $FullAt 'Full'
$i = 1
foreach ($t in $DifferentialAt) { New-BackupTask "DiagnosticLabs Backup (differential $i)" $t 'Differential'; $i++ }
