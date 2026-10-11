# CLAUDE.md

Guidance for Claude Code in this repository. The README describes the solution and what is built; this file holds the rules and the way of working.

## What this is
The rewrite of the legacy WPF "DiagnosticLabs" app as a clean .NET 10 solution: `src/DiagnosticLabs.Domain`, `.Application` (services, no UI), `.Infrastructure` (EF Core 10, SQL Server, migrations), `.Wpf` (screens, printing), tests in `tests/DiagnosticLabs.Application.Tests` (xUnit). The new database is `DiagnosticLabsV2` on `DDDAVID\MSSQLSERVER01`, filled once by `db/migrate-from-legacy.sql`.
The app exists so the client can **print their result forms**: printouts follow the client's own forms.

## Hard rules
- The legacy repo (`D:\Work\004-Others\Diagnostic-Labs`) and the legacy database `DiagnosticLabsDB` are **read only** and must stay running.
- **Never commit or push until the user says "Push all"** (see the `push-all` skill). Report, then wait.
- Test migrations on a scratch database first (`verify-migration` skill), never directly on `DiagnosticLabsV2`.
- Screens must fit **1366x768** (very large forms may scroll vertically; the footer buttons stay fixed).
- Do not drop columns the client no longer shows: hide them and list them in `docs/HIDDEN-FIELDS.md`.
- Use absolute paths for file edits from PowerShell (a relative write once emptied two files).
- A running DiagnosticLabs app locks `src/DiagnosticLabs.Wpf/bin`: build that project with `-o "$env:TEMP\outwpf"` (a lock is not a code error).

## Commands
```powershell
dotnet build                                   # whole solution (close the app first, or build Wpf with -o as above)
dotnet test                                    # all tests
dotnet dotnet-ef migrations add <Name> --project src/DiagnosticLabs.Infrastructure
dotnet dotnet-ef migrations script --idempotent --project src/DiagnosticLabs.Infrastructure --no-build -o db/schema.sql
powershell -File tools\UiHarness\Run-Check.ps1 -Mode <mode>      # offscreen UI check, writes PNGs (render-check skill)
```

## Skills in `.claude/skills`
- `add-lab-form`: a brand new result module (table, menu, billable service, screen, printout, migration, legacy script, tests).
- `rework-lab-form`: change an existing form from client feedback (renames from the database up, hidden fields, saved defaults).
- `verify-migration`: prove a migration on a scratch database in both directions.
- `render-check`: run the offscreen UI check and look at the screens and printouts.
- `push-all`: how to commit and push when asked.

## How the client's requests are handled
1. **Analyze first** when a PDF/photo/sketch arrives (read it, list fields and doubtful handwriting, compare with what exists, ask the few questions that change the build) and wait for the answers. When told "Do not proceed yet", only summarize the plan.
2. A hand-drawn sketch gives the content; the look follows the existing printouts (logo and letterhead, yellow title bar, patient block, black lines, Remarks box, signatories flowing down). A printed form is followed more literally.
3. Verify before reporting: tests, migration on scratch data, and **look at the rendered screen and printout**.
4. Say plainly what is still open (unclear readings, prices to set, permissions to grant).

## Conventions worth knowing
- Lab result forms share `LabResultService<,,>` / `LabResultViewModel<,,>`: header (patient snapshot, registration link, signatories + licences, second Med. Tech), Defaults (saved defaults per module in `ModuleDefaults`), dirty tracking (leaving a changed form asks Save / Don't save / Cancel), printing through `PrintableReport` + `ReportDocumentBuilder`.
- Modules are rows in `Modules` (ids in `ModuleIds`); a billable service is linked through `Modules.ServiceId`, which is what puts a chip on the Home list.
- Lists maintained by the client live in `LookupValues` through the entry builder (`EntryFields`); signatory lists also carry a licence number (`EntryDetail.Signatory`).
- Printing: US Letter, 816x1056 px. Wrapped text grows downward (`PageDrawing.PutFlow`), single-line values shrink to fit (`PutFit`).
- `TreatWarningsAsErrors` is on.
