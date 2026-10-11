---
name: verify-migration
description: Prove an EF migration for DiagnosticLabs is safe before it touches DiagnosticLabsV2 - apply it to a scratch database that holds old-style rows, check every value landed in the right place, roll it back and check again. Use for any migration that renames, moves, adds or seeds data.
---

# Verify a migration on a scratch database

Never test on `DiagnosticLabsV2` (the real new database) or `DiagnosticLabsDB` (the legacy one: READ ONLY, must stay running). Use a throw-away database such as `DL_MigTest` on the same server (`DDDAVID\MSSQLSERVER01`).

## Steps (PowerShell or Bash; `$S` = server, `$C` = connection string for the scratch db)
1. Build once so `--no-build` has fresh assemblies: `dotnet build` (or `dotnet build src/DiagnosticLabs.Infrastructure`).
2. Create the scratch database at the migration BEFORE the one under test:
   `dotnet dotnet-ef database update <previous migration> --project src/DiagnosticLabs.Infrastructure --connection "$C" --no-build`
3. Seed rows in the OLD shape, with a **distinct value in every column** that the migration touches (so a swapped column shows), plus any rows the migration reads (entry lists in `LookupValues`, saved defaults in `ModuleDefaults`, a Fecalysis detail with the second Med. Tech...). `LabReports` needs `ReportType` (the enum NAME as text), `PatientCode`, `PatientName`, `DateRequested`, `CreatedAtUtc`, `UpdatedAtUtc`, `IsDeleted`; the detail row's key is `LabReportId`.
   - If the migration seeds a module/service/defaults, first copy `ModuleTypes` and `Modules` from `DiagnosticLabsV2` into the scratch db (`INSERT ... SELECT ... FROM DiagnosticLabsV2.dbo.Modules`, `ServiceId` NULL). The migrations are guarded by `IF EXISTS (SELECT 1 FROM Modules WHERE Id = 6)`, so without Modules the seeding is skipped (that is correct for a fresh empty database, but it is not what you want to test here).
4. Apply the migration under test (`dotnet dotnet-ef database update --project ... --connection "$C" --no-build`), then SELECT and compare every value with what you seeded.
5. Roll back (`database update <previous migration>`), SELECT again: values and structure are back, module rows removed.
6. Drop the scratch database (`ALTER DATABASE ... SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ...`).
7. Only then apply to `DiagnosticLabsV2` and re-run the data checks read-only.

## sqlcmd tips (they cost time before)
- Always pass `-I` (quoted identifiers): inserts into tables with filtered indexes fail without it. Use `-C -E -b`.
- Passing a script file by a long absolute path through Git Bash failed with a confusing "-E and -U" error: `cd` into the folder and use a relative file name, or send small statements with `-Q`.
- Quote JSON in SQL files, not on the command line.
- The runner `tools/UiHarness/Run-Check.ps1 -StartAt <migration>` does steps 2-4 for the UI side.

## What to look for
- Renames: EF pairs columns by order; verify the pairs (the Albumin/Sugar swap).
- Moves: data copied BEFORE the old column is dropped.
- Seeds: existing saved defaults are kept (`JSON_MODIFY` only where the key is missing); superscripts use `NCHAR`.
- Down: really restores the old shape.
- Large data steps: row counts before and after.
