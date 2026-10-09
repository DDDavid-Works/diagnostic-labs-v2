# Database review and proposed standard

Reviewed: the SSDT project `DiagnosticLabsDatabase` (35 tables, 7 views, post-deployment seed scripts) and the live `DiagnosticLabsDB` (SQL Server, compat level 160, FULL recovery).

## Decisions (owner, 2026-10-09) and what was built

| # | Decision | Status |
|---|---|---|
| 1 | Header + detail tables for lab reports, because a few reports differ from the rest | Built. `LabReports` holds the shared header; 11 detail tables keep every analyte column. APE and MER (the outliers) keep their own extra fields (`BirthDate`, `CivilStatus`, `ContactNo`, `DepartmentOrAgency`, signers) in their detail tables. |
| 2 | `AuditLog` table for history | Built. Written by `AuditLogInterceptor` in the same transaction as the change. Passwords are redacted and blobs are summarised. |
| 3 | Split `IsActive` (reference lists) from `IsDeleted` (records) | Built. Global query filter hides deleted rows. |
| 4 | New database + data migration from the legacy DB | Schema built as EF Core migration `InitialCreate`; the legacy-to-new migration script is built and has been run (see below). |
| 5 | No external tools depend on current names | Names were cleaned up freely (see below). |

Differences from the original proposal, found while building it:
- **Composition, not inheritance, for lab reports.** EF's table-per-type inheritance would join all 11 detail tables whenever the report list is queried. Instead `LabReports` is a normal table with a `ReportType` column, and each detail table is a 1:1 child keyed by `LabReportId`. Listing and paging touch only the header.
- **`ViewOnly` removed from `UserPermissions`** instead of replaced by `AllowView`: having a row already means the user can open the module, so the flag was redundant.
- **Three lookup tables merged** into `LookupValues` (with a `Kind`); `LabResultsDefaults` became `ModuleDefaults`.
- `Patients.Age` is only a free-text fallback now, used when `DateOfBirth` is unknown (otherwise computed from `DateOfBirth`), `Gender` renamed to `Sex`, `Payments.IsCharge` became `Type`.
- Lab `DateRequested` is now required (legacy allowed null); the migration falls back to the row's created date.
- `LabReports.PatientRegistrationId` is nullable: legacy results recorded before registrations existed (`PatientRegistrationId = 0`) keep their patient and have no registration. New reports always get one (enforced in the service layer).
- The payment check allows a zero amount for a `Charge` (the legacy data has one), but not for a `Payment`.
- Audit foreign keys are not indexed (they are never searched on); every other foreign key is.

Verified: the generated script (`db/schema.sql`) applies cleanly to an empty SQL Server database (38 tables, 85 foreign keys, 12 check constraints, 33 indexes), check constraints reject bad data, and 27 automated tests pass, including soft delete, audit log contents and password redaction.

**Ground rule from the owner:** every lab data field is retained (analyte columns, normal-value columns, remarks, technologist/pathologist, photo). Changes below affect common/audit columns, keys, types, constraints, indexes, views, and project hygiene.

## 1. Audit and common fields (the main ask)

### What exists today
Every one of 32 tables repeats the same five columns, with no enforcement:

| Column | Problem |
|---|---|
| `IsActive BIT` | Means two different things: "deactivated" (Services, Items, Users) and "deleted" (Patients, Payments, results). No record of who/when/why. |
| `CreatedByUserId`, `UpdatedByUserId BIGINT DEFAULT 0` | No FK to `Users`. `0` is a fake user (25 of the 31 patients in the dev database have `0`). |
| `CreatedDate`, `UpdatedDate DATETIME DEFAULT GETDATE()` | Local server time (ambiguous across DST/PCs), 3.33 ms precision, set by app code in the legacy app, so inconsistent. |
| (missing) | No concurrency token, so two users editing the same registration silently overwrite each other. |
| (missing) | `CompanySetups` has only Updated*; `Modules`/`ModuleTypes` have none. |

### Proposed standard
A single base class in the domain, stamped by the EF `SaveChanges` interceptor (already built):

| Column | Type | Notes |
|---|---|---|
| `CreatedAtUtc` | `datetime2(3)` NOT NULL, default `SYSUTCDATETIME()` | UTC; convert to local only in the UI. |
| `CreatedByUserId` | `bigint` NULL, FK to `Users` | `NULL` = system/seed. Legacy `0` is migrated to `NULL`. |
| `UpdatedAtUtc` | `datetime2(3)` NOT NULL | |
| `UpdatedByUserId` | `bigint` NULL, FK to `Users` | |
| `RowVersion` | `rowversion` | Optimistic concurrency on all mutable business tables (Patients, PatientRegistrations, Payments, results, Packages, Services, Items). |

Two different lifecycle concepts instead of one overloaded flag:

| Kind of table | Columns | Tables |
|---|---|---|
| **Reference lists** that a user can switch off | `IsActive bit` (business meaning: "no longer offered") | Services, Packages, Items, ItemLocations, Companies, Departments, Discounts, Users, lookup entries |
| **Records** that can be removed | `IsDeleted bit`, `DeletedAtUtc`, `DeletedByUserId`, optional `DeleteReason` | Patients, PatientRegistrations, Payments, all lab results. A global EF query filter hides deleted rows by default. |

Why: today "inactive" and "deleted" cannot be told apart, and deleting a payment or lab result leaves no trace of who removed it.

### Real audit trail (recommended for PHI and money)
`CreatedBy/UpdatedBy` only records the last change. For medical and financial data, add history:
- **System-versioned temporal tables** on `Patients`, `PatientRegistrations`, `Payments`, `Users`, `UserPermissions` and the lab header table (see section 3). SQL Server records every prior row version for free, and with `UpdatedByUserId` you can answer "who changed this and what was it before".
- Keep `Photo` (varbinary) out of history tables (see section 4), otherwise history bloats.
- Alternative if you do not want temporal: an `AuditLog` table (`EntityName`, `EntityId`, `Action`, `ChangedByUserId`, `ChangedAtUtc`, `OldValues`, `NewValues` JSON) written by the same interceptor. Cheaper on storage, easier to query across tables.

I recommend temporal for the five core tables plus the interceptor `AuditLog` for everything else only if you need it later.

## 2. Keys, integrity and indexes

| Finding | Fix |
|---|---|
| Only 21 FKs. All 11 lab-result tables have `PatientId` and `PatientRegistrationId` **without FKs** and nullable. `DefaultValues.ModuleId` has no FK. | Add FKs. Make both NOT NULL for new data (check existing rows first). |
| `0` used as a "none" default on required FK columns (13 columns, e.g. `PatientRegistrations.CompanyId`, `PackageServices.PackageId`). It only works because a sentinel `Companies` row has `Id = 0` (WALK-IN). | Remove the `DEFAULT 0` everywhere. For required FKs there is no default. For optional links use `NULL`. "No company" is `NULL`; the WALK-IN sentinel row is not migrated. |
| **Zero indexes** besides PKs. No index on any FK, none on search columns that the paged grids filter by. | Index every FK. Add indexes for `Patients(PatientName)`, `Patients(PatientCode)`, `PatientRegistrations(InputDate)`, `Payments(PaymentDate)`, lab `DateRequested`. Use filtered indexes (`WHERE IsDeleted = 0`) on large tables. |
| **No unique constraints.** Nothing stops a duplicate `Users.Username`, `Patients.PatientCode`, `PatientRegistrations.RegistrationCode`, or two `UserPermissions` rows for the same user+module. (No duplicates exist today, so this is safe to add.) | Add unique indexes: `Users(Username)`, `Patients(PatientCode)`, `PatientRegistrations(RegistrationCode)`, `UserPermissions(UserId, ModuleId)`, `PackageServices(PackageId, ServiceId)`. |
| **No CHECK constraints.** Money and percentages are unconstrained. | `Price >= 0`, `PaymentAmount > 0`, `DiscountPercentage BETWEEN 0 AND 100`, `Quantity >= 0`; on `DiscountDetails` exactly one of `Amount`/`Percentage` is set. |
| Registration codes are produced by a view (`LatestCodeNumbers`) that string-splits and `ISNUMERIC`-parses every existing code to find the max. Two users registering at the same moment can get the same code. | Replace with a `CodeSequences` table (`Prefix`, `NextNumber`) incremented atomically (`UPDATE ... OUTPUT`) inside the registration transaction. Keep the existing code format. |
| `UserPermissions` has `ViewOnly` plus four `Allow*` flags, which can contradict each other. | Replace `ViewOnly` with `AllowView` so all five are independent capabilities. |
| Constraint names are inconsistent (`Patient_Id`, `PaymentId_Id`, `PatientRegistration_Id`). | Standard names: `PK_Patients`, `FK_Payments_PatientRegistrations`, `UQ_`/`IX_` prefixes. |

## 3. Lab result tables (fields retained)

Eleven tables repeat the same header: `PatientId`, `PatientRegistrationId`, `PatientCode`, `PatientName`, `CompanyOrPhysician`, `Age`, `Sex`, `DateRequested` (APEs/MERs call it `DateInputted`), `MedicalTechnologist`, `Pathologist`, `Remarks`, `Photo`, plus audit. Only the analyte columns differ. The `LabResults` view exists purely to stitch them back together with a 10-way `UNION`.

**Recommended: header + detail tables.**
- New `LabReports` table: the shared header and audit columns, plus a `ServiceType` and a `Status`.
- Each existing lab table keeps **all its analyte columns unchanged** and shares the `LabReports.Id` as its primary key (1:1). In EF this is table-per-type mapping.
- Wins: one FK target and one list for search/paging, so the `LabResults` view and its UNION disappear; patient/registration FKs are declared once; header fixes (e.g. `DateInputted` vs `DateRequested`, `Sex` vs `Gender`) are done once; photos and print state attach to one place.
- Cost: a one-time data move per lab table (split header columns out). Row counts are tiny today (a handful), so this is the cheapest moment to do it.

**Conservative alternative** (if you prefer zero structural change): keep eleven tables as they are, standardize header column names/types and add the missing FKs, and keep the view as `UNION ALL`.

Points that apply to either option:
- `PatientCode`/`PatientName`/`Age`/`Sex`/`CompanyOrPhysician` on a result are a **snapshot at the time of the report**, which is legitimate for a medical document. Keep them, document them as snapshots, and always populate them from the same source (the `LabResults` view currently takes `PatientCode` from the result row for two tables and from `Patients` for the rest).
- Analyte result and normal-value columns (`*Result`, `*NValue`, 47 of them) are free-text `nvarchar`. Retain as-is. A later, optional step is a `ReferenceRanges` table to prefill normal values.
- APE examination flags are `nchar(4)`; keep, but use `nvarchar` for consistency.
- Rename for clarity in the new schema (reports are being rebuilt anyway): `ClinicalChemistries`/`1`/`2` to `ClinicalChemistryPanel*` with meaningful names, `Serologies`, `Immunologies`, `StoolFecalyses`, `Urinalyses` to a consistent `*Results` or singular-per-report convention. The scaffold already produced awkward names like `Urinalyse`, `Ape`, `Mer`.

## 4. Data types

| Now | Proposed | Reason |
|---|---|---|
| `DATETIME` (80 columns) | `datetime2(3)`; `DateOfBirth` as `date` | Wider range, better precision semantics, UTC convention. |
| `Patients.Age nvarchar(50)` ("20 years old") | Drop from `Patients`, compute from `DateOfBirth`. Keep on lab results as the snapshot text. | Stale the day after it is stored. |
| `varchar` in `DefaultValues`, `SingleLineEntries`, `MultiLineEntries`, `LabResultsDefaults` | `nvarchar` | Mixed with `nvarchar` elsewhere; names and remarks can contain non-ASCII. |
| `varbinary(max)` `Photo` on 9 lab tables, `Logo` on `CompanySetups` | Separate `LabReportPhotos(LabReportId, Content, ContentType)` and `FILESTREAM`/file storage if images are large | Wide rows are slow to scan and bloat temporal history; EF currently loads photos whenever it loads a result. |
| `Gender` on `Patients`, `Sex` on results; `CivilStatus nvarchar(20)` | One name (`Sex`), constrained to a known set via lookup or CHECK | Inconsistent vocabulary. |
| `Payments.IsCharge bit` | `PaymentType` (`Payment`, `Charge`, later `Refund`) | A bit cannot grow; add `PaymentMethod` and `ReferenceNumber` when you want receipts reconciled. |
| Four overlapping lookup/default tables (`DefaultValues`, `SingleLineEntries`, `MultiLineEntries`, `LabResultsDefaults`) | One `LookupValues` (`ModuleId`, `FieldName`, `Title`, `Value`, `Kind`) | Same concept stored four ways. |

Money columns are already `DECIMAL(18,4)`; keep.

## 5. Users and security

- Rename `Users.Password` to `PasswordHash` (the new app already maps it). Add `MustChangePassword`, `LastLoginAtUtc`, `FailedLoginCount`, `LockoutEndUtc`.
- Passwords are unsalted SHA-1 today; the new hasher upgrades them at login (see AUDIT.md). A column expansion to hold the longer PBKDF2 format is already within `NVARCHAR(200)`.
- Seed data inserts a default `Admin` with a trivial password. Replace it with a deployment step that requires setting the first admin password and sets `MustChangePassword`.
- Patient data is sensitive: enable **TDE** (transparent data encryption) on the database and encrypt backups; consider Always Encrypted for `Patients.Address`/`ContactNumbers` only if there is a compliance need.
- The legacy app connects as `sa`. The new app should use a dedicated least-privilege SQL login (or Windows auth) with `db_datareader`/`db_datawriter`, no DDL rights at runtime.
- Recovery model is FULL: confirm log backups are scheduled, otherwise the log grows forever. For a single site that can accept losing up to the last backup, `SIMPLE` is fine.

## 6. Views

| View | Issue | Proposal |
|---|---|---|
| `LabResults` | `UNION` (not `ALL`) forces a dedupe sort; `ROW_NUMBER() OVER (ORDER BY Id)` is meaningless across tables and not stable for paging. | Replaced by `LabReports` (section 3). If kept: `UNION ALL`, no `ROW_NUMBER`. |
| `LatestCodeNumbers` | String parsing, race-prone. | Replace with `CodeSequences` (section 2). |
| `PatientRegistrationDetails` | `RIGHT JOIN` followed by `WHERE p.IsActive = 1` is really an inner join. | Rewrite as `INNER JOIN`. |
| `PatientCompanies`, `PatientRegistrationBatches`, `PatientRegistrationPayments`, `PaymentDetails` | Exist to feed grid screens; unfiltered by soft-delete consistently. | Review during the slice that uses each; most can become EF projections (`Select`) with server-side paging instead of views. |

## 7. Project hygiene

- **Sample data is deployed to every database.** `Script.PostDeployment.sql` includes `Patients.sql`, `PatientRegistrations.sql`, `StoolFecalyses.sql` (demo data). Split into *reference data* (modules, module types, permissions, defaults, services: always) and *sample data* (dev/test only, guarded by a flag).
- Seed scripts are `IF NOT EXISTS` inserts that never update. Use idempotent `MERGE`/upsert so reference data changes ship.
- `Companies` gets `Id = 0` via identity insert as a sentinel; remove that (section 2).
- Project target is SQL Server 2016 (`Sql130`) while the database runs at compat 160. If we keep a database project, move it to the SDK-style `Microsoft.Build.Sql` (builds with `dotnet build`, works in CI) targeting `Sql160`.
- Build artefacts (`bin`, `obj`, `.dbmdl`, `.jfm`) are present in the folder; ensure they are gitignored.

## 8. How to roll it out

The changes (UTC, renames, header split, removed defaults) are not compatible with the legacy app, so I propose **a new database populated by a one-time migration**, not editing `DiagnosticLabsDB` in place:

1. Define the new schema from the domain model with **EF Core migrations**. This reverses my earlier "keep SSDT" advice: since the entities and base classes are now the single source of truth and the schema is changing anyway, a second hand-maintained copy (SSDT) is exactly the manual sync problem the legacy project had. Views, temporal config, filtered indexes and seed data all fit in migrations.
2. Write an idempotent **ETL script** (`legacy -> new`) per table: `datetime -> datetime2` converted to UTC, audit `0 -> NULL`, lab header split, `Companies` sentinel mapped. Verify with row counts and checksum queries per table.
3. Run the ETL against a restored copy of production, compare report outputs old vs new, then cut over. The legacy database stays untouched as the fallback.

## 9. Decisions I need from you

1. Lab tables: header + detail (recommended) or conservative?
2. History: temporal tables on core tables (recommended), or `AuditLog` table, or just the standard columns?
3. Soft-delete split (`IsActive` vs `IsDeleted`): agree?
4. New database + ETL (recommended) vs evolve `DiagnosticLabsDB` in place?
5. Do you need to keep any legacy column/table names because of external reports or tools? Otherwise I will rename freely.
