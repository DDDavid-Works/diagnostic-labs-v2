# Custom Form Builder: specification (draft)

Lets the client create simple result forms without a developer. Complex forms that must match a printed original stay with the `add-lab-form` skill.

## 1. Form definition

A form is made of four parts, set in a designer screen:

1. **Service**: dropdown of billable services not yet linked to a module. Sets `Modules.ServiceId`, which puts the chip on the Home list.
2. **Header**
3. **Body**
4. **Footer**

The form also has a **name** (menu entry and printout title).

### 1.1 Header
- A grid of 3 columns and 2 rows (6 cells for the 6 header fields; changed from 3x3, whose third row was always empty). Each cell is empty or one header field.
- Fields: Patient Code, Patient Name, Age, Sex/Gender, Company, Date Requested.
- A field already used in the grid is not offered in the other cells.
- No other header fields in v1 (decided: Registration No., Address, Physician, Date Reported and Specimen are not included).

### 1.2 Body
**A. Group columns (0 to 2)**
- Each group has an optional **title** shown above it.
- Each group has three subcolumns: **Test Name**, **Result Value**, **Reference/Normal Values**.
- Reference is free text and can be **hidden per group**. Hidden means not shown on screen and not printed; entered data is kept.
- Rows: up to **30 per group**.
  - Rows are added, deleted and reordered by **dragging**.
  - A row is either a **test row** (name required) or a **section row** (a heading with no result).
  - Result Value is free text. Each test row can optionally use an **Entry Builder** list, which makes it a dropdown on the real form.
  - There is no unit column.

**B. Textboxes (0 to 3)**
- Multiline, with a required label.
- Optional on the form like any other field.
- Printed below the groups.

### 1.3 Footer
- Up to 3 signatories, taken from the existing signatory lists (licence number included). Built so far: each slot is a role, **at most 2 Medical Technologists and 1 Pathologist**, because the result is stored on the existing `LabReports` signatory columns (Medical Technologist, second Medical Technologist, Pathologist). Allowing other combinations would need new columns.
- One switch to disable the licence number, for the whole form (decided).
- Remarks box: on or off.

## 2. Storage
Existing forms keep their own tables. Custom forms use generic tables.

| Table | Content |
|---|---|
| `CustomForms` | id, name, `ModuleId` (unique), `IsActive`. The service is linked through `Modules.ServiceId`, as for the built-in forms |
| `CustomFormVersions` | form id, version number (unique per form), definition as JSON (header grid, groups, rows, textboxes, footer) |
| `CustomFormReports` | the result: shares its key with a `LabReports` row (`ReportType = CustomForm`), **version id**, values as JSON keyed by row id |

- The patient snapshot, registration link, signatories and Remarks of a result live on the existing `LabReports` row, so Home, Batch Print and soft delete work as for the other forms. The current version of a form is its highest version number.
- Built in step 1 (migration `CustomForms`). The Custom Lab Form designer module is id 32; modules of the forms the client makes take ids from 1000 (`ModuleIds.FirstCustomForm`).
- Every row, group and textbox has a stable GUID, so renaming never breaks saved results. A GUID without dashes (32 characters) also fits `LookupValues.FieldName` (50), which is how a row's dropdown list is stored.
- Saving a changed definition creates a new version. Old results open and print with the version they were saved with.
- Defaults reuse `ModuleDefaults` (saved per module).
- Dirty tracking and Save / Don't save / Cancel follow the existing lab forms.

## 3. Fit with the existing code
The custom forms are added **next to** the existing code; the current forms, `ModuleIds` and `EntryFields` stay as they are.
- `EntryField` is a plain record and `LookupValues` is keyed by `ModuleId` + `FieldName`, so a custom form's dropdown is an `EntryField` created at runtime with the form's module id. `EntryService` needs no change.
- Custom forms need `Modules` rows created at runtime. **To check:** how module ids are assigned; use a reserved range (for example 1000 and above) so they never collide with ids added later by `add-lab-form`.
- **To check:** `EntryService` permission check (`hostModuleId`) works for a custom form's own module id.
- Small additions (a branch for "custom form", existing branches unchanged) in: `MainViewModel` (menu), `LabResultPrinters` / `PrintableReport` / `ReportDocumentBuilder` (printing), `LabReport` and the Home chips (`RegistrationBoardService`) if needed.
- A new module row needs permissions for users to be granted, so the menu entry appears only for roles that have it.
- Printing goes through `PrintableReport` and `ReportDocumentBuilder` (US Letter, 816x1056). One page only in v1: the designer warns when header, body and footer do not fit.
- Screens must fit 1366x768. The designer and the entry form scroll vertically with fixed footer buttons.

## 4. Suggested build order
1. Data model, migration (scratch database first) and the tests. **Done.**
2. Designer: service, header, footer. **Done** (`FormDesignerService`, `FormDefinition`, Custom Lab Form designer screen under Management; check it with `Run-Check.ps1 -Mode designer -StartAt 20261012104153_StatementsOfAccount`). Saving a changed design adds the next version; a stale save (someone saved a newer version meanwhile) is refused. A new form gets its module row (copied from Clinical Microscopy) and shows in the menu after the next sign-in; users need access in Users.
3. Designer: body (groups, rows with drag, textboxes). **Done.** `BodyDefinition` / `GroupDefinition` / `RowDefinition` / `TextboxDefinition` are part of `FormDefinition` (validated: 2 groups, 30 rows per group, 3 text boxes, names required, unique ids; a version saved before the body existed reads as an empty body). Rows are test rows or heading rows, reordered by dragging the grip. Every test row's result will be an Entry Builder dropdown list named by the row id (step 4), so nothing per row is stored for that. The "does it fit one page" warning needs the real printout layout, so it comes with step 5.
4. Entry screen built from the definition, with Defaults and dirty tracking. **Done.** `CustomFormResultService` (one class for every custom form; the form is chosen through `CustomFormContext`, which the screen's `ModuleBoundRunner` sets before asking for the service) builds on `LabResultService`, so the registration link, patient snapshot, duplicate warning, search and soft delete are shared with the built-in forms. A result stores the design version it was saved with and is shown with it (a note says so when it is not the newest); a new result uses the newest. Each test row's result is an Entry Builder list named by the row id, each text box has saved texts, reference values and text box texts can be saved as defaults, and the Home chip of the form's service opens it (the board counts only that form's results). The signatories map to the existing LabReports columns. **Not done yet (step 5):** the printout. Print says "not ready" until step 5.
5. Printout and preview. **Done.** `CustomFormLayout` (WPF) draws the page from `PrintableReport.Custom`: letterhead and yellow title bar, the header grid, the groups side by side (a row grows when a value has to wrap), the text boxes and Remarks box that grow with their text, and the designed signatories centred, with the electronic note. The spacing lives in `CustomFormPage` (Application), which the designer's **one-page warning** also uses (it assumes one-line rows and empty text boxes, so it is a lower bound: it warns, it does not block a save). With Remarks and two signatories with licence numbers, 28 rows in one group fit and 29 do not. Checked with `Run-Check.ps1 -Mode designer ...` (printouts `custom-print-1.png` and `custom-print-2.png`; the drawn page end equals the estimate for a one-line design). **Batch Print** offers custom forms too: their service is listed when the user may print the form, a search counts only results saved with that form (all custom forms share one report type, so it checks the form through the result's design version), and `LabResultPrinters` makes a `CustomFormResultService` told which form it is for.
6. Versioning and the editing rules.
7. Render check of screens and printouts.

## 5. Decisions
1. Licence number switch applies to the whole form.
2. One printed page only in v1.
3. A form is never deleted; it can be deactivated (hidden from the menu and Home, service freed, old results stay printable).
4. The designer is its own module, **Custom Lab Form designer**, with its own permissions.
5. Copying a form as a starting point: later, not v1.
6. No extra header fields in v1.
