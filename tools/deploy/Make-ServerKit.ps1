<#
.SYNOPSIS
  Packs what the server needs into one folder and zip: the deploy scripts, the db scripts and StationSetup.exe.
  Copy the zip to the server (USB), unzip it, and run Install-Server.ps1 -CheckOnly from there.
#>
[CmdletBinding()]
param([string]$OutputDir)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $OutputDir) { $OutputDir = Join-Path $repo 'artifacts\server-kit' }

if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $OutputDir 'db') | Out-Null
Get-ChildItem (Join-Path $repo 'tools\deploy') -File | Copy-Item -Destination $OutputDir
Copy-Item (Join-Path $repo 'db\schema.sql'), (Join-Path $repo 'db\seed.sql'), (Join-Path $repo 'db\migrate-from-legacy.sql'), (Join-Path $repo 'db\verify-migration.sql') -Destination (Join-Path $OutputDir 'db')
Copy-Item (Join-Path $repo 'docs\DEPLOYMENT.md'), (Join-Path $repo 'docs\STATION-SETUP.md') -Destination $OutputDir
dotnet publish (Join-Path $repo 'tools\StationSetup\StationSetup.csproj') -p:PublishProfile=SingleFile
if ($LASTEXITCODE -ne 0) { throw "StationSetup publish failed (exit $LASTEXITCODE)." }
Copy-Item (Join-Path $repo 'artifacts\station-setup\StationSetup.exe') -Destination $OutputDir

$zip = "$OutputDir.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $OutputDir '*') -DestinationPath $zip
Write-Host "Server kit: $OutputDir and $zip"
exit 0
