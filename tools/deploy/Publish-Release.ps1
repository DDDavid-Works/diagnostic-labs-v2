<#
.SYNOPSIS
  Builds a station release and (optionally) copies it to the server's update share.

.DESCRIPTION
  Runs the Station publish profile (self-contained win-x64), stamps the version, and writes
  version.txt LAST so a launcher never sees a half-copied release as complete.

.EXAMPLE
  .\Publish-Release.ps1                                   # build only, to artifacts\publish
  .\Publish-Release.ps1 -ShareTarget \\SERVER\DiagnosticLabs\current
  .\Publish-Release.ps1 -Version 1.0.0 -Zip                 # build and pack artifacts\release-1.0.0.zip for the server
#>
[CmdletBinding()]
param(
    # Defaults to a timestamp, e.g. 2026.10.11.1430. Pass your own to tag a release (e.g. 1.0.3).
    [string]$Version = (Get-Date -Format 'yyyy.MM.dd.HHmm'),
    # Update share folder the stations read from. Omit to build only.
    [string]$ShareTarget,
    # Also pack the release into artifacts\release-<version>.zip, to carry to the server (USB or remote session).
    [switch]$Zip
)

$ErrorActionPreference = 'Stop'

$repo    = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repo 'src\DiagnosticLabs.Wpf\DiagnosticLabs.Wpf.csproj'
$publish = Join-Path $repo 'artifacts\publish'

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

dotnet publish $project -p:PublishProfile=Station -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }

if (Test-Path (Join-Path $publish 'appsettings.Local.json')) {
    throw 'appsettings.Local.json is in the release. Stations keep their own; remove it before shipping.'
}

if ($ShareTarget) {
    New-Item -ItemType Directory -Force -Path $ShareTarget | Out-Null

    # Mirror everything except version.txt; /MIR also removes files dropped from the app.
    robocopy $publish $ShareTarget /MIR /XF version.txt /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) { throw "robocopy to the share failed (exit $LASTEXITCODE)." }

    # Publish the version only once all files are in place.
    Set-Content -Path (Join-Path $ShareTarget 'version.txt') -Value $Version -Encoding ASCII
    Write-Host "Released $Version to $ShareTarget"
}
else {
    Set-Content -Path (Join-Path $publish 'version.txt') -Value $Version -Encoding ASCII
    Write-Host "Built $Version in $publish (no share given, nothing copied)."
    if ($Zip) {
        $zipFile = Join-Path $repo "artifacts\release-$Version.zip"
        if (Test-Path $zipFile) { Remove-Item $zipFile -Force }
        Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $zipFile -CompressionLevel Optimal
        Write-Host "Packed $zipFile"
    }
}

# robocopy returns 1-7 for success; do not leak that as the script exit code.
exit 0
