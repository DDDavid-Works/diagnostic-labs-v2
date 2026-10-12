<#
.SYNOPSIS
  Runs one offscreen UI check (a "mode" of tools/UiHarness) against a throw-away scratch database and prints its [ok]/FAIL lines.

.DESCRIPTION
  1. builds the harness to a temp folder (a running DiagnosticLabs app locks its own bin folders, so never build over them),
  2. creates the scratch database DL_UiCheck from the EF migrations (never touches DiagnosticLabsV2 or the legacy database),
  3. seeds an admin user and a company row (so printouts have a letterhead),
  4. runs the harness: the mode is chosen by the END of the output folder name, so the folder is <Out>\<Mode>,
  5. always drops the scratch database.

  Screenshots and printout PNGs are written to the output folder (default: $env:TEMP\ui-check\<Mode>).

.PARAMETER Mode
  Which check to run: physical, microscopy, fecal, renames, dirty, home, ... (see the dispatch lines near the top of Program.cs).

.PARAMETER StartAt
  Optional migration to apply FIRST (the one before the migration you are testing). The lab modules are then copied from -ModulesFrom
  (read only) and the remaining migrations are applied, so a migration that seeds a module, service or saved defaults is exercised for real.

.EXAMPLE
  .\Run-Check.ps1 -Mode microscopy -StartAt 20261010164750_PhysicalExamination
#>
param(
    [Parameter(Mandatory)][string]$Mode,
    [string]$Server = $(if ($env:DL_SQL_SERVER) { $env:DL_SQL_SERVER } else { 'DDDAVID\MSSQLSERVER01' }),
    [string]$StartAt,
    [string]$ModulesFrom = 'DiagnosticLabsV2',
    [string]$Out
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$db = 'DL_UiCheck'
$connection = "Server=$Server;Database=$db;Trusted_Connection=True;TrustServerCertificate=True"
$exeDir = Join-Path $env:TEMP 'uiharness-out'
if (-not $Out) { $Out = Join-Path (Join-Path $env:TEMP 'ui-check') $Mode }
if (-not $Out.EndsWith($Mode)) { throw "The output folder must end with the mode name ('$Mode'): the harness picks the check from it." }

function Invoke-Sql([string]$query, [string]$database = $db) {
    & sqlcmd -S $Server -d $database -E -C -b -I -Q $query | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $query" }
}

function Update-Database([string]$target) {
    $arguments = @('dotnet-ef', 'database', 'update')
    if ($target) { $arguments += $target }
    $arguments += @('--project', (Join-Path $root 'src\DiagnosticLabs.Infrastructure'), '--connection', $connection, '--no-build')
    & dotnet @arguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'dotnet ef database update failed' }
}

function Remove-ScratchDatabase {
    & sqlcmd -S $Server -E -C -Q "IF DB_ID('$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END" | Out-Null
}

Push-Location $root
try {
    Remove-ScratchDatabase
    & dotnet build (Join-Path $PSScriptRoot 'UiHarness.csproj') -o $exeDir --nologo -v q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The harness did not build.' }

    if ($StartAt) {
        Update-Database $StartAt
        # the lab modules (read only copy), so the migration under test finds them like it will in the real database
        Invoke-Sql "INSERT INTO ModuleTypes (Id, ModuleTypeName, Icon, SortOrder, IsActive, IsAdmin) SELECT Id, ModuleTypeName, Icon, SortOrder, IsActive, IsAdmin FROM [$ModulesFrom].dbo.ModuleTypes; INSERT INTO Modules (Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, ServiceId) SELECT Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, NULL FROM [$ModulesFrom].dbo.Modules;"
    }

    Update-Database $null
    Invoke-Sql "SET IDENTITY_INSERT Users ON; INSERT INTO Users (Id, Username, FullName, PasswordHash, IsAdmin, MustChangePassword, FailedLoginCount, CreatedAtUtc, UpdatedAtUtc, IsActive) VALUES (1, N'Admin', N'Test Admin', N'x', 1, 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1); SET IDENTITY_INSERT Users OFF; INSERT INTO CompanySetups (CompanyName, Address, ContactNumbers, Email, Code, CreatedAtUtc, UpdatedAtUtc) VALUES (N'BIO ASSAY DIAGNOSTIC CENTER', N'Angeles City', N'888-8888', N'bio@yahoo.com', N'BADC', SYSUTCDATETIME(), SYSUTCDATETIME());"

    if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
    New-Item -ItemType Directory -Force $Out | Out-Null
    $env:DL_NO_ERROR_DIALOG = '1'   # the global error box must never pop up on a desktop during a check
    $env:ConnectionStrings__DiagnosticLabs = $connection   # the start-up version check inside the harness must read the scratch database, not DiagnosticLabsV2
    & (Join-Path $exeDir 'UiHarness.exe') $Out $connection | Where-Object { $_ -match 'ok\]|FAIL|binding errors|Unhandled|Exception|LOGGED ERROR' }
    $failures = $LASTEXITCODE
    Write-Host "`nOutput folder: $Out"
    if ($failures -ne 0) { Write-Host "$failures check(s) failed." -ForegroundColor Red }
    exit $failures
}
finally {
    Remove-ScratchDatabase
    Pop-Location
}
