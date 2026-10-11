---
name: add-lab-form
description: Add a brand new lab result module (a new form with its own table, menu entry, billable service, screen and printout) to DiagnosticLabs. Use when the client sends a new form (sketch, PDF or photo) that is its own module, such as Physical Examination or Clinical Microscopy.
---

# Add a new lab result form

Use `ClinicalMicroscopy*` (simplest) and `PhysicalExamination*` (sections with dates, two columns) as the templates: copy their shape, do not invent a new one.
For changes to an EXISTING form use the `rework-lab-form` skill instead.

## 0. Analyze first, then agree the plan (do not build before the user answers)
Read the client's file (PDFs/photos: extract the embedded image and look at it; HEIC can be converted with the Windows HEIF decoder via `System.Windows.Media.Imaging`). Reply with: the layout, every field as read (flag unclear handwriting), how it differs from what exists, and a short list of questions. Always settle:
- menu name and whether it is standalone (own table, own entry lists, no link to other forms), billable service + Home chip, hand-drawn sketch or printed form;
- each field: dropdown list (entry builder, starts empty) or typed; which values are saved defaults (normal values / reference ranges); dates; Remarks yes/no;
- signatories: two (no licences) or three (second Med. Tech + licences, `HasSecondTechnologist`), and the footer note (`ElectronicNote`).
Printing follows the client's form: for a hand-drawn sketch use its content in the style of the other printouts (see the memory note `new-form-approach`); for a printed form follow its positions.

## 1. Build (every file below has a sibling to copy)
1. **Domain** `src/DiagnosticLabs.Domain`: add the value to `LabReportType` (Lab/LabReport.cs); `Lab/Reports/<Name>Report.cs` (`: LabReportDetail`, nullable string columns, dates as `DateOnly?`); its `IEntityTypeConfiguration` with `HasMaxLength` in `Infrastructure/Persistence/Configurations/LabReportDetailConfigurations.cs`.
2. **Application**: `ModuleIds.<Name> = <next free id>` (ids up to 30 are taken; Modules are NOT identity), `ReportLayout.<Name>` (LabResults/PrintableReport.cs), the module->type map `LabResultModules` in `Board/RegistrationBoardService.cs`, `services.AddScoped<I<Name>Service, <Name>Service>()` in `DependencyInjection.cs`, an `EntryFields.<Name>Remarks` in `Entries/EntryService.cs`.
3. **Service** `LabResults/<Name>Service.cs`: one static catalog class describing the fields once (names = entity property names), data records carrying values by name, columns read/written by reflection (see `ClinicalMicroscopyService`), validation with `Max(...)`, `PrintFields`, `Title`, `Layout`, `HasSecondTechnologist`, `FooterText`.
4. **Wpf**: `ViewModels/<Name>ViewModel.cs` (`LabResultViewModel<,,>`; `ChoiceFields`, `ExtraDefaultFields` for saved defaults, `ResetDetail`, `BuildInput`, `ShowFields`); `services.AddTransient` in `App.xaml.cs`; the page in `MainViewModel.Pages`; a `DataTemplate` in `Views/CrudTemplates.xaml`; a layout class + `case` in `Printing/ReportDocumentBuilder.cs` using the shared `PageDrawing` helpers (`PutHeader`, `PutTitleBar`, `PutPatientBlock`, `PutFit`, `PutFlow`, `PutThreeSignatories`). Wrapped text must grow downward (`PutFlow`), single-line values shrink to fit (`PutFit`).
5. **Screen must fit 1366x768**: very large forms may scroll vertically, the footer buttons stay fixed.
6. **Migration** (`dotnet dotnet-ef migrations add <Name> --project src/DiagnosticLabs.Infrastructure`): keep the generated `CreateTable`, then append guarded SQL (copy the block from `*_ClinicalMicroscopy.cs`) that creates the billable **Service** (price 0), the **Modules** row (copy the flags from Urinalysis, module 6) and the saved **ModuleDefaults** (`JSON_MODIFY` only for keys that are missing). Guard with `IF EXISTS (SELECT 1 FROM Modules WHERE Id = 6)`: a fresh empty database must stay empty of Modules because the legacy script refuses a non-empty target. Add a `Down` that removes the module rows. Regenerate `db/schema.sql`: `dotnet dotnet-ef migrations script --idempotent --project src/DiagnosticLabs.Infrastructure --no-build -o db/schema.sql`.
7. **Legacy-data script**: append the SAME SQL before `-- ===== Summary =====` in `db/migrate-from-legacy.sql` AND as `Emit '...'` lines in `tools/Generate-LegacyMigration.ps1` (fresh databases get Modules from the legacy script, not from the migration).
8. **Tests** `tests/DiagnosticLabs.Application.Tests/LabResults/<Name>ServiceTests.cs`: a **guard test** that every catalog name is a column of the entity (a mistyped name once produced wrong column names and was only caught this way), save/read-back with a distinct value per field, empty/blank values, too-long values, printout fields/signatories/footer, print permission, and `LabResultModules.ReportTypeOf`.

## 2. Verify (all of it, before saying "done")
- `dotnet test` (all pass).
- Migration: use the `verify-migration` skill (scratch database, both directions), then apply to `DiagnosticLabsV2`.
- Screen and printout: add a mode to `tools/UiHarness` and run it with the `render-check` skill; LOOK at the PNGs (long values, empty form, no overlap with the logo).
- README line, memory note, and the user is told: the service price is 0 (set it in Services), non-admin users need access in Users.

## Gotchas
- A running DiagnosticLabs app locks `src/DiagnosticLabs.Wpf/bin`; build with `dotnet build src/DiagnosticLabs.Wpf -o "$env:TEMP\outwpf"`.
- `TreatWarningsAsErrors` is on (primary-constructor parameters captured AND passed to the base class give CS9107: copy to a field).
- Do not commit or push until the user says "Push all" (see the `push-all` skill).
