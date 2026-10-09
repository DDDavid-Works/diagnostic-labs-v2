# Printing proposal

How the new app prints lab results, receipts and reports, replacing Crystal Reports.

## What the old app does

- One Crystal Reports template (`.rpt`) per result type: Stool/Fecalysis, Urinalysis, Hematology, Immunology, Serology, Clinical Chemistry (three variants), APE and MER, plus a Company Setup sample. The templates are binary files designed in the Crystal designer.
- `PrintViewModel` loads the template, feeds it the result record (plus the company name, address, contacts and e-mail as parameters) and shows it in the Crystal viewer, which has its own print and export buttons.
- Findings worth knowing:
  - **Pregnancy Test has no template** in the old project's `Reports/LabResults` folder (the code expects `PregnancyTestReport.rpt`). It may never have been printable.
  - APE and MER have a "Page 2" screen each; their templates are the biggest (65 and 29 fields).
  - There is no print screen for payments or registrations, and the **Sales Report** and **Statement of Accounts** menu items exist but are not built yet.
- Crystal Reports needs the SAP runtime and the .NET Framework. It does not run in a .NET 10 app, so it has to be replaced, not carried over.

## Options

| Option | Layout is written in | Preview / print | Cost and catches |
| --- | --- | --- | --- |
| **A. WPF documents** (`FixedDocument`) | XAML/C# building blocks | Built-in `DocumentViewer` preview and `PrintDialog`; "Microsoft Print to PDF" for PDF | No dependency, no license. Page breaks for long tables are ours to handle (the result forms are one or two pages, so this is manageable). |
| **B. QuestPDF** | Fluent C# | PDF file; needs a viewer control and a PDF-to-printer step | Excellent paging and tables. Free only below a revenue threshold (check the current terms before adopting). Extra pieces for preview and printing. |
| **C. Report designer product** (FastReport, Stimulsoft, DevExpress) | Visual designer | Built-in viewer | Closest to the Crystal workflow (drag-and-drop), but paid licenses. |
| **D. RDLC / ReportViewer** | Visual designer in Visual Studio | WinForms viewer hosted in WPF | Legacy technology, awkward in .NET 10. |
| **E. Keep Crystal through a helper .NET Framework process** | Existing `.rpt` files | Existing viewer | Keeps the old layouts untouched, but keeps the SAP runtime install, 32/64-bit issues and a second app to ship. It also blocks the data model change: the templates are bound to the old column names. |

## Recommendation: A, WPF documents, with a shared report model

The result forms are fixed one- or two-page layouts, not long flowing reports, which is what WPF documents handle well. Nothing to license, nothing extra to install, and preview, printer choice and PDF export come with Windows.

The part that makes 11 layouts cheap is a **data-first design**:

1. **`IReportService` (Application layer, unit-tested).** Builds a plain `ReportData` for a result: the company header (name, branch, address, contacts, e-mail, logo), the patient block, and the result as sections of rows (label, value, unit, reference range). No WPF here, so every number on paper is covered by tests.
2. **A small set of layout building blocks (WPF layer).** Page header, patient block, section table, remarks box, signature block, footer. Each result type is a short description of its sections and fields, rendered by the same engine. APE and MER, which are form-like, get their own page layouts built from the same blocks.
3. **One preview window** with Print, Save as PDF and printer/paper options, reused by results, receipts and reports. A "printed by / when" line can be added to the footer and recorded in the audit log if you want a trail of who printed what.

Things this gives us beyond the old app: a payment receipt, the Sales Report and Statement of Accounts through the same preview window, and printouts that are consistent across all result types.

## Phasing

1. **Engine and preview window, proved on a payment receipt** (small, and it settles paper size, header and logo handling).
2. **Hematology**: the first lab result screen built together with its printout, so each result type is delivered "enter it and print it".
3. The other result types, simplest first.
4. Sales Report, Statement of Accounts.

## What I need from you

1. **Paper size and orientation** the lab actually prints on (A4, Letter, Legal/Folio, half-page?), and whether any printer is a thermal or receipt printer.
2. **Letterhead:** is the header (name, address, logo) printed by us on plain paper, or is the paper pre-printed?
3. **Sample printouts** (PDF or photo) of each result type from the old app. I will match their layout rather than guess, and they tell me which fields really appear on paper.
4. **Pregnancy Test:** should it print at all? There is no old template to copy.
5. **Signatures:** who signs a result (medical technologist, pathologist, doctor), and are those names typed per result or fixed in Company Setup?
6. **PDF needs:** is saving a PDF to e-mail results to patients or companies wanted, or paper only?
7. Whether the **printed by / when** footer line and an audit entry per print are wanted.
