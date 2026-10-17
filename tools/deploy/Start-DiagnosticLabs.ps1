<#
.SYNOPSIS
  Station launcher: brings the local copy up to date from the server share, then starts the app.

.DESCRIPTION
  Reads the share path from launcher.json next to this script:
      { "SharePath": "\\\\SERVER\\DiagnosticLabs\\current" }

  Compares SharePath\version.txt with the local version.txt. When they differ it copies the
  release (robocopy) into the install folder, never touching appsettings.Local.json (the
  station's connection string), and starts the app. If the share is unreachable the installed
  copy still starts, so a server hiccup does not stop the lab.

  First run: if appsettings.Local.json is missing it is copied from SharePath\..\config\appsettings.Local.json.
#>
[CmdletBinding()]
param(
    [string]$SharePath,
    # Per-user, no admin rights needed. Logs live elsewhere (LocalAppData\DiagnosticLabs\logs).
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'DiagnosticLabs\app')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

function Show-Message([string]$text, [string]$icon = 'Warning') {
    [void][System.Windows.Forms.MessageBox]::Show($text, 'Diagnostic Labs', 'OK', $icon)
}

$exe       = Join-Path $InstallDir 'DiagnosticLabs.Wpf.exe'
$localVer  = Join-Path $InstallDir 'version.txt'
$localConf = Join-Path $InstallDir 'appsettings.Local.json'

try {
    if (-not $SharePath) {
        $cfg = Join-Path $PSScriptRoot 'launcher.json'
        if (Test-Path $cfg) { $SharePath = (Get-Content $cfg -Raw | ConvertFrom-Json).SharePath }
    }

    $shareVer = if ($SharePath) { Join-Path $SharePath 'version.txt' } else { $null }
    $shareUp  = $shareVer -and (Test-Path -LiteralPath $shareVer)

    if ($shareUp) {
        $latest  = (Get-Content -LiteralPath $shareVer -Raw).Trim()
        $current = if (Test-Path $localVer) { (Get-Content $localVer -Raw).Trim() } else { '' }

        if ($latest -ne $current) {
            # Cannot replace files under a running copy.
            $running = Get-Process -Name 'DiagnosticLabs.Wpf' -ErrorAction SilentlyContinue |
                Where-Object { $_.Path -eq $exe }
            if ($running) {
                Show-Message "A new version ($latest) is available.`nClose Diagnostic Labs, then open it again to update."
                return
            }

            New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

            # No version.txt while copying: an interrupted copy is retried next launch, never run.
            Remove-Item $localVer -Force -ErrorAction SilentlyContinue

            # /MIR drops files removed from the app; /XF keeps the station's own config.
            robocopy $SharePath $InstallDir /MIR /XF version.txt appsettings.Local.json /NFL /NDL /NJH /NJS /NP | Out-Null
            if ($LASTEXITCODE -ge 8) { throw "Update copy failed (robocopy exit $LASTEXITCODE)." }

            Set-Content -Path $localVer -Value $latest -Encoding ASCII
        }
    }

    if (-not (Test-Path $localVer) -or -not (Test-Path $exe)) {
        Show-Message "Diagnostic Labs is not installed on this PC and the server share could not be reached.`n`nCheck the network and try again.`n($SharePath)" 'Error'
        return
    }

    # First run on this PC: seed the connection string from the server.
    if (-not (Test-Path $localConf) -and $SharePath) {
        $seed = Join-Path (Split-Path $SharePath -Parent) 'config\appsettings.Local.json'
        if (Test-Path -LiteralPath $seed) { Copy-Item -LiteralPath $seed -Destination $localConf }
        else { Show-Message "No database settings found for this PC.`nAsk your administrator to add appsettings.Local.json." }
    }

    # Tell the app where the share is and how to restart through this launcher (its "new version ready" notice and Restart now button).
    if ($SharePath) { $env:Update__SharePath = $SharePath }
    $env:Update__Launcher = Join-Path $PSScriptRoot 'Start-DiagnosticLabs.cmd'

    Start-Process -FilePath $exe -WorkingDirectory $InstallDir
}
catch {
    Show-Message "Could not start Diagnostic Labs.`n`n$($_.Exception.Message)" 'Error'
}
