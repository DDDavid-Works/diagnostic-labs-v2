---
name: rework-lab-form
description: Change an EXISTING lab result form in DiagnosticLabs from client feedback (rename fields from the database up, add or remove fields, new layout or signatories) without losing saved data. Use for requests like "rename Albumin to Protein", "add a test to Hematology", "rework the Fecalysis form from this sketch".
---

# Rework an existing lab form

For a brand new module use `add-lab-form`. Always analyze the request first and summarize the plan; ask only what you cannot decide (e.g. keep or hide a removed field).

## Rules
- **"Change from database up"** means the column, entity property, configuration, service, view model, XAML, printout, tests, saved entry lists and saved defaults all change, with a migration that keeps the data.
- **Never trust EF's scaffolded renames.** EF paired `Albumin->Glucose` and `Sugar->Protein` the wrong way round. After `migrations add`, rewrite `Up`/`Down` by hand with explicit `RenameColumn(name, table, newName)` pairs and check every pair. Order for moved data: add new columns, copy, then drop old ones (EF drops first).
- **Names that live in data** must be renamed too, in the same migration:
  - entry lists: `UPDATE LookupValues SET FieldName = ... WHERE ModuleId = <id> AND FieldName = ...`
  - saved defaults (`ModuleDefaults.Defaults` JSON): `REPLACE(Defaults, N'"OldKey', N'"NewKey')` for that module only.
- **Removed fields are hidden, not dropped** unless the client says otherwise: remove them from the service/screen/printout but keep the columns, make sure saving does not touch them (write a test), and add a row to `docs/HIDDEN-FIELDS.md`.
- **New saved defaults** (units, reference ranges) are seeded in the migration with `JSON_MODIFY` only for keys that do not exist yet, so an administrator's own values are never overwritten. Use `NCHAR(...)` for special characters (x10^9: `NCHAR(215)`, superscripts `NCHAR(185)`, `NCHAR(178)`, `NCHAR(8313)`).
- **Legacy-data script**: columns renamed since the legacy schema need the mapping in `tools/Generate-LegacyMigration.ps1` (`$renamedColumns`) AND in the generated `db/migrate-from-legacy.sql` (the destination column list of that table's INSERT; the SELECT keeps the legacy names). Data renames (entry lists, defaults) are appended as SQL before `-- ===== Summary =====` in both files.
- Shared pieces: the second medical technologist + licence is a shared header field; turn it on with `HasSecondTechnologist` and `LabResultBottom ShowSecondTechnologist/ShowLicenses`. Licence numbers come from signatory entry lists (`EntryDetail.Signatory`).

## Steps
1. Plan (what changes, what stays hidden, questions).
2. Code across all layers (search the old names with Grep over `src tests tools docs`; skip `Migrations/`).
3. Migration: `dotnet dotnet-ef migrations add <Name> --project src/DiagnosticLabs.Infrastructure`, then hand-write Up/Down; regenerate `db/schema.sql`.
4. Tests: update the service tests; add one for every behaviour that must not regress (hidden fields kept, renamed key).
5. `verify-migration` (rows with distinct values in every renamed column; check each value is in the right column, then roll back and check again), apply to `DiagnosticLabsV2`, run `render-check`, look at the printout.
6. README / `docs/HIDDEN-FIELDS.md` / memory; tell the user what changed and what they must confirm (unclear handwriting).
