# Rehearsal checklist

A full dry run of the installation on a **spare PC (as the server) and one or two station PCs**, before anything is installed at the client. The aim is to find out what breaks on a clean Windows machine, with no developer tools and no Visual Studio. Tick each box as you go and write down anything that did not work first time; that list becomes the client's install notes.

Background and reasoning: `docs/DEPLOYMENT.md`. Scripts: `tools/deploy/`, `db/`.

## Before you start

- [ ] **Server PC:** Windows 11 Pro, wired to the same switch as the stations, fresh or a clean user account.
- [ ] **Station PC(s):** Windows 10 22H2 or Windows 11 (V2 cannot run on Windows 7), 64-bit. If you have one, also try one low-spec PC (4 GB RAM, hard disk) and note how the start-up feels.
- [ ] A **build machine** (your PC) with the repo, to run `Publish-Release.ps1`.
- [ ] A **second disk or USB drive** for the backup copy, and a **printer** that the lab would use (US Letter).
- [ ] Decide the names now and keep them: server name `________`, SQL login `________`, share `\\SERVER\DiagnosticLabs`.

## A. Server

> **Shortcut:** `toolsdeployInstall-Server.ps1` does A1 (network part) to A5 in one go and is safe to run twice. Build the kit with `toolsdeployMake-ServerKit.ps1`, copy the zip to the server, then run `.Install-Server.ps1 -CheckOnly` first. The manual steps below stay as the reference for what it does and for checking its result.

### A1. SQL Server
- [ ] Install SQL Server Express (2022), mixed-mode authentication, and set the `sa` password (keep it somewhere safe; the app never uses it).
- [ ] SQL Server Configuration Manager: enable **TCP/IP**, set the fixed port **1433** (clear "TCP Dynamic Ports"), restart the SQL service.
- [ ] Windows Firewall: inbound rule for TCP 1433, **Private/Domain profile and the LAN only**.
- [ ] Windows power: sleep and hibernate **off**; active hours set; the SQL service starts **automatically**.

### A2. Create the database
Run on the server, from a folder holding a copy of `db\schema.sql` and `db\seed.sql`:
```powershell
sqlcmd -S .\SQLEXPRESS -E -C -Q "CREATE DATABASE DiagnosticLabsV2"
sqlcmd -S .\SQLEXPRESS -d DiagnosticLabsV2 -E -C -I -b -i db\schema.sql
$env:DL_ADMIN_PASSWORD = '<temporary password>'
sqlcmd -S .\SQLEXPRESS -d DiagnosticLabsV2 -E -C -I -b -i db\seed.sql
$env:DL_ADMIN_PASSWORD = $null
```
- [ ] `schema.sql` finishes with no error (exit code 0).
- [ ] `seed.sql` prints "Seed complete".
- [ ] Running `seed.sql` a second time **refuses** ("Target database is not empty").

### A3. The application's SQL login
- [ ] Create a login for the app, with rights on this database only:
```sql
CREATE LOGIN dl_app WITH PASSWORD = '<strong password>', CHECK_POLICY = ON;
USE DiagnosticLabsV2;
CREATE USER dl_app FOR LOGIN dl_app;
ALTER ROLE db_datareader ADD MEMBER dl_app;
ALTER ROLE db_datawriter ADD MEMBER dl_app;
```
- [ ] From a **station**: `sqlcmd -S SERVER,1433 -U dl_app -P ... -d DiagnosticLabsV2 -C -Q "SELECT COUNT(*) FROM Modules"` returns 33.
- [ ] Note any extra right the app turns out to need (write it here: ________). The login must not be able to change the schema.

### A4. Update share
- [ ] Create `C:\DiagnosticLabs` with sub-folders `current` and `config`; share it as `DiagnosticLabs` (Read for stations, Change only for the admin).
- [ ] Put `appsettings.Local.json` in `config` (connection string with the `dl_app` login, `Server=SERVER,1433;...;TrustServerCertificate=True`).
- [ ] The `config` folder is readable by the stations, so use the app login only, never `sa`.

### A5. Backups
- [ ] Create the backup folder (e.g. `D:\Backups`) and give the **SQL Server service account** Modify rights on it.
- [ ] Run `tools\deploy\Backup-Database.ps1 -Server .\SQLEXPRESS -BackupDir D:\Backups -CopyDir E:\labs` by hand: a full backup appears in both places, `backup.log` says "written and verified".
- [ ] Run it with `-Type Differential`.
- [ ] Run `Register-BackupTasks.ps1` (elevated); in Task Scheduler **run each task once manually**: Last Run Result = `0x0`.
- [ ] Unplug the copy drive and run again: the task must show as **failed**, the local backup must still exist.
- [ ] **Restore test:** restore the backup under another name (`DiagnosticLabsV2_Test`) using the steps in `DEPLOYMENT.md`; compare the row counts of `Users`, `Modules`, `Services`; drop the test database.

## B. Release

On the build machine:
- [ ] `tools\deploy\Publish-Release.ps1 -Version 1.0.0 -ShareTarget \\SERVER\DiagnosticLabs\current` finishes, "Released 1.0.0".
- [ ] The share holds `DiagnosticLabs.Wpf.exe` and `version.txt`, and **no** `appsettings.Local.json`.
- [ ] Note the size and how long the copy took over the real network: ________

## C. Station (repeat per PC)

### C1. First install
- [ ] **Credentials for the update share** (workgroup, no domain): on the station run once, as the Windows user who will use the app, `cmdkey /add:SERVER /user:SERVERdlupdate /pass:<password shown by Install-Server.ps1>`; then `dir \SERVERDiagnosticLabsrrent` must list files without a prompt. Windows stores it per user, so repeat for each Windows user on that PC.
- [ ] Copy the three launcher files (`Start-DiagnosticLabs.cmd`, `Start-DiagnosticLabs.ps1`, `launcher.json`) to a local folder, e.g. `C:\DiagnosticLabs-Launcher`. Edit `launcher.json` to the real server name.
- [ ] Create a desktop shortcut to `Start-DiagnosticLabs.cmd` (name "Diagnostic Labs"; for the icon, choose the installed `%LOCALAPPDATA%\DiagnosticLabs\app\DiagnosticLabs.Wpf.exe` after the first run, which now carries the app icon).
- [ ] **No .NET or other runtime is installed on this PC beforehand** (check `dotnet --list-runtimes` returns nothing, to prove the self-contained build).
- [ ] Double-click the shortcut: the app copies to `%LOCALAPPDATA%\DiagnosticLabs\app`, `appsettings.Local.json` appears there, and the login screen opens.
- [ ] Time from click to login (first run, second run): ________ / ________
- [ ] A console window flashes briefly: acceptable? ________

### C2. Sign in and set up
- [ ] Sign in as `Admin` with the temporary password: the app asks for a **new password** and accepts it.
- [ ] Sign out and in with the new password; the old temporary password no longer works.
- [ ] Menu shows all groups (Registration, Sales, Lab Results, Management, Reports, Settings). Items and Item Locations are hidden.
- [ ] Set the **price of each service** (Management > Services) and fill **Company Setup** (name, address, logo).
- [ ] Create a second, non-admin user with limited permissions; sign in as that user and confirm the menu shrinks.
- [ ] Check every screen fits **1366x768** (the target monitors) without cutting off buttons.

### C3. A day of work (run it as the clerk would)
- [ ] Register a patient, then a registration with several services.
- [ ] Enter at least one result of each form that is built (Fecalysis, Urinalysis, Hematology, Immunology, Serology, Pregnancy Test, Clinical Chemistry, Physical Examination, Clinical Microscopy, Medical Examination Report (print it: two pages), APE) and save.
- [ ] Take a payment (full and partial) and check the Home list shows the correct state.
- [ ] **Sales Report** (Reports menu): print one day and a range, then one company and one of its batches; the totals match the registrations. Print it on the real printer: it must come out **landscape** and uncut (the preview is landscape; confirm the printer driver follows it). Also print to PDF.
- [ ] **Batch Print** (Reports menu): pick a company and Hematology (or another built form) and Search: every patient of the company with that service is listed with Yes/No and the date the result was inputted. Print the whole company, then one batch: one page per patient with a result, sorted by name. Print ten or more pages on the real printer and to one PDF. Give a non-admin user Batch Print + the form's Print permission and check the service only shows up for them when both are granted.
- [ ] **Statement of Accounts** (Reports menu): generate a company statement (one batch, then the whole company, with and without a date range) and check it lists only registrations marked **Charged** and that Gross, Discount and Net add up to what the client expects. Generate a patient statement (Paid and Balance match Payments). Find both under Saved statements and reprint: same number and figures. Take a payment afterwards and reprint: the saved statement must **not** change. Void one (the reprint shows VOID) and Regenerate another (new number, the old one void). Print a long one (50+ lines) on the real printer and to PDF: header on every page, TOTALS only on the last. Give a non-admin user Create, Print and Delete separately and check each action follows its permission.
- [ ] **Custom Lab Form** (Management menu, admin only): make a small form (service not yet linked to a screen, a header, one group with a few rows, one with an Entry Builder list, a text box, signatories), save, **sign out and in** (the new menu entry appears after the next sign-in), give a non-admin user access in Users and check the entry shows only for them. Enter and save a result from it, open it from the Home chip of its service, and print it (one page, letterhead and signatories correct; the designer warns when the design does not fit one page). Change the design, save (a new version), and check an old result still opens and prints as it was saved. Then check Batch Print: the form's service is listed for a user with Batch Print + the form's Print permission, patients with a result show Yes, and a batch prints one page per patient. Take a backup and restore it under another name: the form and its results come back with it.
- [ ] **Print** a result and a receipt on the real printer: US Letter, logo and signatories correct, nothing cut off. Also print to PDF.
- [ ] Two stations at once: the same patient open on both; save on one, then on the other: the second gets the concurrency message, no data lost (the README lists this check for Patients; note whether other screens behave the same).
- [ ] Close the app with unsaved changes: it asks to save.

## D. Failure drills (what the client's staff will meet)

- [ ] **Server off:** switch the server off or unplug the station's cable, then start the app. Expected: "cannot reach the database"; no crash. Plug back in, start again: works.
- [ ] **Share off, app installed:** stop sharing or disconnect, start via shortcut: the installed copy still starts.
- [ ] **Share off, brand-new station:** a PC that has never run the app and cannot see the share shows the "not installed" message.
- [ ] **New release:** publish 1.0.1 (change anything small). A station that is **closed** updates on next launch (check `%LOCALAPPDATA%\DiagnosticLabs\app\version.txt`), keeps its `appsettings.Local.json`, and keeps working.
- [ ] A station with the app **already open** when 1.0.1 is published: next shortcut click says to close the app first and does not corrupt the install.
- [ ] **Database ahead:** with a station still on 1.0.0, run a newer `schema.sql` that adds a migration; the old program shows "older than the database", the shortcut then updates it. (Needs a throwaway migration; do this on the rehearsal database only.)
- [ ] **Database behind:** restore a backup taken before a migration; the app shows "database is older", names the missing migration in `%LOCALAPPDATA%\DiagnosticLabs\logs`.
- [ ] **Wrong password in the config:** edit `appsettings.Local.json`; the message must be understandable, and the log must hold the real reason.
- [ ] Server reboot with nobody logged in: SQL Server comes back by itself, stations reconnect.
- [ ] Windows Update restart on the server (force one): how long was the lab without the database? ________

## E. Wrap-up

- [ ] Delete the rehearsal data or restore a clean "just seeded" backup, so the real install starts from `schema.sql` + `seed.sql` again.
- [ ] Update `docs/DEPLOYMENT.md` with every surprise found; list unresolved items below.
- [ ] Collect screenshots of each screen for the user manual while the data is fresh.

### Findings

| # | Step | What happened | Fix / decision |
|---|---|---|---|
| 1 | | | |
