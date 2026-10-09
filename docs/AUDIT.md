# Audit of legacy DiagnosticLabs (Phase 0)

Source audited: `D:\Work\004-Others\Diagnostic-Labs` at commit `860acb3`, plus the live `DiagnosticLabsDB` (read-only).

## Size

| Area | Count |
|---|---|
| UI C# (code-behind + ViewModels + misc) | ~17,100 LOC (code-behind 5,150; ViewModels 7,230) |
| BLL | ~4,300 LOC, 26 service classes (`LabResultsBLL` alone is 1,191) |
| DAL entities | ~6,300 LOC, 41 `DbSet`s |
| XAML files | 57 (windows + user controls) |
| Crystal reports | 11 `.rpt` |
| Tables / views | 35 tables (incl. `__RefactorLog`), 7 views, 21 FKs, 0 non-PK indexes |

## Findings

### Security (fix regardless of the rewrite)
1. **Passwords use unsalted SHA-1.** `CommonFunctions.HashPassword` hashes in the UI and `UsersBLL.IsLoginSuccess` compares the hash in the query. Unsalted SHA-1 is trivially crackable. Plan: PBKDF2/Argon2 with a per-user salt, upgrading existing hashes transparently on next successful login.
2. **Database credentials are committed.** `App.config` contains `sa` credentials, and `Trusted_Connection=True` is mixed with a user id/password. The `sa` account should not be used by an app. Rotate it.
3. **Seed data ships a default `Admin` user with a trivial password** (`Scripts/Data/Users.sql` inserts it unhashed, so it likely doesn't even match the hash login path until changed). The rewrite should force a password change on first login.

### Architecture
- Entities do triple duty: EF entity, `INotifyPropertyChanged` (36 files), and `IDataErrorInfo` validation (32 files), plus `[NotMapped]` UI-only fields. This is why price/amount string shadow properties kept appearing.
- BLL is table-CRUD, one class per table, each newing its own `DbContext`. 128 synchronous EF calls in the BLL; no `async` anywhere in UI or BLL.
- Static `Globals` is used ~69 times in 22 files (current user, admin flag, permission caches).
- Hardcoded log paths in `App.config` (`D:\Work\ZZZ-Logs\`).
- 10 lab-result code-behinds are ~150–216 lines each and look near-duplicates (copy-paste per lab type).

### Database
- Lab result tables are very wide, one column pair per analyte: `APEs` 72 cols, `Hematologies` 38, `MERs` 36, `ClinicalChemistries` 35, `Urinalyses` 33.
- 47 `*Result` / `*NValue` columns are `NVARCHAR`, so result values and normal ranges are free text. Keep as-is (data model is retained), but model them as strings in the domain and parse only where needed.
- Patient identity is denormalized into result rows (`PatientCode`, `PatientName`, `Age`, `Sex`) next to `PatientId`.
- 32 tables have `CreatedByUserId`/`UpdatedByUserId`/`CreatedDate`/`UpdatedDate`, with **no FK** to `Users`. In the rewrite these become a base `AuditableEntity` populated by a `SaveChanges` interceptor.
- Soft delete via `IsActive` everywhere (no global query filter today).
- `DATETIME` rather than `datetime2`; `DECIMAL(18,4)` for money (fine).
- Table naming irregularities: `ClinicalChemistries`, `ClinicalChemistries1`, `ClinicalChemistries2`, `Serologies`, `Immunologies`, `StoolFecalyses`, `Urinalyses`; the scaffold singularizes some badly (`Urinalyse`, `StoolFecalyse`, `Ape`, `Mer`).
- Live DB and SSDT agree on the 35 tables / 7 views.
- Lab-result tables are (almost) empty in the dev DB; `Patients` (31) and `Modules` (28) have the most data. Real data volumes are unknown.

### Reports
11 Crystal reports: APE, ClinicalChemistry (x3), CompanySetup, Hematology, Immunology, MedicalExamination, Serology, StoolFecalysis, Urinalysis. Replacement tool still to be decided (QuestPDF is the leading candidate).

### Packages worth dropping
Fody/PropertyChanged, EF Core 3.1 and its Microsoft.Extensions 3.1 chain, `System.Configuration.ConfigurationManager`, Crystal Reports assemblies, and the large bindingRedirect block.

## Scaffold status
`dotnet ef dbcontext scaffold` ran successfully against the live DB into
`src/DiagnosticLabs.Infrastructure/Persistence/Scaffolded/`: 42 draft types (35 tables + 7 keyless views, minus `__RefactorLog`) and `ScaffoldedDbContext`. These are **raw, as-generated** drafts to mine for the clean model, not final code.

## Proposed new solution layout
```
src/
  DiagnosticLabs.Domain          entities, enums, value types, rules (no EF, no UI)
  DiagnosticLabs.Application     use-case services, DTOs, validators, abstractions
  DiagnosticLabs.Infrastructure  EF Core 10, configurations, interceptors, reporting, logging
  DiagnosticLabs.Wpf             Views, ViewModels (CommunityToolkit.Mvvm), DI host
tests/
  DiagnosticLabs.Application.Tests
```

## Suggested vertical-slice order
1. Foundation: host, config, logging, EF context, auth (hashed passwords + migration of existing users)
2. Patients, Companies, Packages, Services, Items
3. Registration, Discounts, Payments (billing math under test)
4. Lab results, grouped by shared shape
5. Reports, Sales, Settings/permissions

## Open decisions
- Report engine replacement.
- Whether to keep SSDT as schema source or switch to EF migrations (recommend keep SSDT).
- WPF only vs. adding an API layer.
