# Deployment proposal

How V2 is installed and kept up to date at the client's site: one server and up to 25 client stations on a local network. This is a proposal for discussion. Nothing described here is built yet unless it says so.

## The site

- **Server:** Windows 11 Pro (requested by the client). It hosts the SQL Server database and the backups.
- **Stations:** up to 25. **About half still run Windows 7.**
- **Sign-in to the database:** SQL login (no domain assumed).
- Stations talk straight to SQL Server over TCP. There is no web or application server.

```
[Win 11 Pro server]   SQL Server Express + backups + update share
        |  LAN, wired, fixed address
[Station x 25]        DiagnosticLabs.Wpf (self-contained)
```

## The Windows 7 problem

V2 targets .NET 10, which needs **Windows 10 1607 or newer** (Windows 11 included). It cannot run on Windows 7, and there is no update or compatibility switch that changes this.

Options for the Windows 7 stations, in order of preference:

| Option | Notes |
|---|---|
| **A. Upgrade the PC** (Windows 10 22H2 or 11) | Clean answer. Check each PC first: 64-bit CPU, 4 GB RAM or more (8 GB better), SSD strongly recommended. Many old Windows 7 PCs fail one of these, so the real cost is hardware. |
| **B. Keep those stations on the old app** | Not an option: the legacy app was never in use at the client, so there is nothing for a Windows 7 PC to keep running. Listed only to say so. |
| **C. Remote Desktop into a host that runs V2** | Needs a Windows Server host with Remote Desktop licences (Windows 11 Pro allows one interactive session). Adds cost and a single point of failure. Not recommended. |

**Decision for the client:** because nothing is in production yet, there is no phase-in. A station either runs V2 (Windows 10/11) or it does not run anything, so every Windows 7 PC that will be used must be upgraded or replaced before it is needed. Ask which stations are actually needed on day one.

## Server

- **SQL Server Express 2022** (free). Limits are 10 GB per database, 1 GB of buffer memory and 4 cores. This is plenty for 25 clerks. Revisit only if the database grows past a few GB.
- **Create the database:** run `db/schema.sql`, then `db/seed.sql`, on an empty `DiagnosticLabsV2` (see "Starting data" below).
- **Network:**
  - Enable TCP/IP and use the fixed port 1433 (turn off dynamic ports; SQL Browser is not needed).
  - Open the firewall for that port to the LAN only.
  - Give the server a DHCP reservation or static IP.
  - Stations connect by server name or IP.
- **App login:**
  - Create one SQL login for the app, with rights on `DiagnosticLabsV2` only (not `sa`).
  - Keep the password out of the repo.
  - Because it sits in a file on each station, give it only what the app needs (read/write on tables, no schema changes).
  - Schema changes are run with a separate administrative login.
- **Windows 11 acting as a server:**
  - Disable sleep and hibernate.
  - Set active hours, and defer feature updates so it does not reboot mid-day.
  - Set the SQL Server service to start automatically.
  - Put the server on a UPS.
  - Note: Windows 11 allows only **20 simultaneous inbound SMB connections**. SQL traffic is not affected, but a shared folder used by 25 stations can hit this limit (see "Updating the app").

### Backups

Express has no SQL Agent, so use Windows Task Scheduler running a PowerShell or `sqlcmd` script:

- A full backup every night, plus a differential or log backup during the day if the client wants less than a day of exposure.
- Copy each backup to a second disk or a NAS. A backup on the same disk as the database is not a backup.
- Keep at least 14 days.
- **Test a restore** before go-live, and again every few months.
- Because of the `AuditLog` table, record changes can usually be traced, but the audit data is only as safe as the backups.

## Stations

### What gets installed

V2 is published **self-contained** (`win-x64`), so the .NET 10 runtime is inside the app folder. A Windows 10/11 station needs **nothing installed first** (no .NET, no Crystal Reports runtime).

Proposed publish settings (to add to `DiagnosticLabs.Wpf.csproj` or a publish profile):

- `RuntimeIdentifier` = `win-x64`
- `SelfContained` = `true`
- No single-file (WPF with single-file is awkward and makes updates slower)
- No trimming (WPF and EF Core use reflection)

Rough size: about 150 MB per release. That is fine on a LAN but is why the update step below copies only when the version changes.

### Per-station configuration

`App.xaml.cs` already loads `appsettings.Local.json` as an optional per-machine override. That is the right place for the connection string:

```json
{
  "ConnectionStrings": {
    "DiagnosticLabs": "Server=SERVERNAME,1433;Database=DiagnosticLabsV2;User Id=...;Password=...;TrustServerCertificate=True"
  }
}
```

- The committed `appsettings.json` currently uses `Trusted_Connection=True`, which only works with a domain or a matching local account. With SQL login it must be overridden on every station.
- Updates must **replace the app files but leave `appsettings.Local.json` alone**.
- Also give the app a per-user log folder (Serilog already writes files).

### Updating the app

25 stations means a release should not need 25 visits. Proposal:

1. Each release is published to a folder on the server (for example `\\SERVER\DiagnosticLabs\current`, read-only for stations).
2. A small **launcher** (a shortcut to a script or tiny exe) on each station:
   - compares the version in `current\version.txt` with the local copy,
   - copies the new files if different (`robocopy`, excluding `appsettings.Local.json`),
   - then starts the app.
3. Stagger or accept that the first launch after a release takes longer. Because the files are copied once per station, the 20-connection SMB limit is only hit if many stations update at the same moment.

Alternatives: an MSIX/MSI installer pushed by hand to each station (simple, but 25 visits per release), or running the app straight from the share (no install, but slow start-up and a hard dependency on the share). The launcher is the best fit for a small site with no IT department.

### Database version check (built)

No separate version table: EF Core already records every applied migration in `__EFMigrationsHistory`, and both `db/schema.sql` and `dotnet ef database update` keep it current. At start-up, before the login screen, the app compares the migrations compiled into it with that history (`SchemaCheck`, `SchemaVersionService`):

| Situation | What the station shows |
|---|---|
| Same migrations | Nothing, the login screen opens. |
| Database lacks migrations (not set up, or an update was not run) | "Ask your administrator to update the database." The log lists the missing migrations. |
| Database has migrations this program does not know (the station has an older program) | "Close it and open Diagnostic Labs from its shortcut so it can update itself." The launcher then copies the new release. |
| Server unreachable, wrong server name or login | "Cannot reach the database. Check the network..." The error goes to the log. |

**Release order** (done by `Update-Server.ps1`, see "Releasing an update"): back up the database, run `db\schema.sql` on it (it is idempotent: it applies only what is missing), publish the new app folder to the share, then stations pick it up on their next launch. Between the second and third step, stations still on the old program see the "older than the database" message and are sent to the launcher, which is the intended behaviour.

A migration is only safe to ship if `schema.sql` also builds an empty database: test that on a scratch database after every migration that uses raw SQL (two migrations once failed here because an `UPDATE` ran on a column added in the same batch).

## Rollout order

1. **Rehearsal on a spare PC:** install SQL Server Express, run `schema.sql`, publish the app, run a station through the launcher.
2. **Pilot:** server plus 2 Windows 10/11 stations for about a week, with staff entering real-looking test registrations. Printing is the thing to check: preview, printer choice and PDF export on the actual lab printers (US Letter).
3. **Go-live:** clear the pilot test data (or restore the clean starting-data backup), then bring stations on in batches. The client's legacy setup data (services and prices, packages, companies, a few test patients) is migrated once, before go-live: see "Migrating the client's legacy data".
4. **Retire the old database** on the client PC: take a last backup of `DiagnosticLabsDB`, keep it somewhere safe for a while, then drop it.

## Open items

- Count and spec the Windows 7 PCs (CPU 64-bit, RAM, disk) to see how many can be upgraded rather than replaced.
- Decide which stations are needed on day one, and have the lab confirm the reference ranges in the seed.
- Confirm the backup target (second disk, NAS or external drive) and who rotates it.
- Decide where `appsettings.Local.json` comes from for a new station (a template file in the update share with the password filled in on site).
- Nothing left to build for deployment. Remaining: rehearsal on a spare PC and the user manual.
- User manual (see below).

## User manual (later)

The client will need a user manual. Suggested outline, so we can collect screenshots as screens settle:

1. Signing in, and the main menu.
2. Registering a patient and picking services (the point-of-sale style flow).
3. Entering and printing each lab result type.
4. Payments, discounts and receipts.
5. Searching and reports.
6. Daily routine for the lab: opening, closing, who to call.
7. For the lab admin: users and permissions, company setup, backups (what runs, how to check it), how updates arrive.

The screens still change, so the manual should be written after the UI is stable. Screenshots can be captured with the offscreen harness in `tools/UiHarness`.

## Tools (built)

All in `tools/deploy/`; the publish profile is `src/DiagnosticLabs.Wpf/Properties/PublishProfiles/Station.pubxml`.

- **`Publish-Release.ps1 [-Version x] [-ShareTarget \SERVER\DiagnosticLabs\current]`**: publishes the self-contained release (about 160 MB), refuses to ship if `appsettings.Local.json` slipped in, mirrors to the share, and writes `version.txt` last.
- **`Update-Server.ps1`** and **`Rollback-Server.ps1`**: the server side of every later release (see "Releasing an update"). They live in the server kit; rebuild it with `Make-ServerKit.ps1` or copy the two files over.
- **`Start-DiagnosticLabs.cmd`** (shortcut target) runs **`Start-DiagnosticLabs.ps1`**, which reads the share path from **`launcher.json`** (edit the server name on site) and:
  - copies the release to `%LOCALAPPDATA%\DiagnosticLabs\app` when the version differs, keeping the station's `appsettings.Local.json`;
  - on first run, seeds that file from `\SERVER\DiagnosticLabs\config\appsettings.Local.json`;
  - starts the installed copy if the server is unreachable;
  - asks the user to close the app first if an update is waiting while it is running.

Station setup is then: copy the `tools/deploy` files (`.cmd`, `.ps1`, `launcher.json`) to a local folder, make a desktop shortcut to the `.cmd`. Put the SQL-login `appsettings.Local.json` in the share's `config` folder (not in the repo).

### Backup tools

- **`Backup-Database.ps1 -BackupDir D:\Backups [-CopyDir \NAS\labs] [-Type Full|Differential] [-Server .\SQLEXPRESS] [-RetentionDays 14]`**: backs up with `CHECKSUM` (compressed when the edition allows), runs `RESTORE VERIFYONLY`, copies to the second location, deletes backups older than the retention (always keeping the newest 3 full ones), logs to `backup.log` and exits non-zero on any failure. Uses .NET, so `sqlcmd` is not needed.
- **`Register-BackupTasks.ps1 -BackupDir D:\Backups -CopyDir \NAS\labs -FullAt 22:00 -DifferentialAt 12:00,16:00`**: creates the Task Scheduler jobs (run once, elevated). It asks for the Windows password so the tasks run while logged off.

Server setup points that are easy to miss:

- The backup folder is written by the **SQL Server service**, not by the person running the script. Give `NT Service\MSSQL$SQLEXPRESS` (or `NT Service\MSSQLSERVER`) Modify rights on it.
- Run the tasks as the administrator who installed SQL Server. `SYSTEM` is not a sysadmin on SQL Server 2022, so a task running as `SYSTEM` fails.
- A failed copy to the second location marks the task failed even though the local backup is good. Check Task Scheduler's "Last Run Result" (should be 0) and `backup.log` every week or two until someone owns this.

### Restoring (practice this before go-live)

On the server, with everyone logged out of the app:

```sql
-- 1. Restore the last full backup, leaving the database ready for a differential
RESTORE DATABASE [DiagnosticLabsV2] FROM DISK = N'D:\Backups\DiagnosticLabsV2_FULL_<stamp>.bak'
    WITH REPLACE, NORECOVERY;
-- 2. Restore the newest differential taken AFTER that full (skip this step if none) and bring it online
RESTORE DATABASE [DiagnosticLabsV2] FROM DISK = N'D:\Backups\DiagnosticLabsV2_DIFF_<stamp>.bak'
    WITH RECOVERY;
-- If there is no differential, instead run:  RESTORE DATABASE [DiagnosticLabsV2] WITH RECOVERY;
```

To rehearse without touching the live data, restore under another name (`RESTORE DATABASE [DiagnosticLabsV2_Test] ... WITH MOVE` for the data and log files) and compare record counts.

## Starting data

`db/schema.sql` creates empty tables, and the app has no built-in seeding, so a new database needs `db/seed.sql` before anyone can sign in. The old development database held only test data, so the seed starts clean instead of copying it.

**Fresh install, in order** (on the server, in PowerShell):

```powershell
sqlcmd -S .\SQLEXPRESS -E -C -Q "CREATE DATABASE DiagnosticLabsV2"
sqlcmd -S .\SQLEXPRESS -d DiagnosticLabsV2 -E -C -I -b -i db\schema.sql
$env:DL_ADMIN_PASSWORD = '<temporary password>'     # 8+ letters/digits, no quotes
sqlcmd -S .\SQLEXPRESS -d DiagnosticLabsV2 -E -C -I -b -i db\seed.sql
$env:DL_ADMIN_PASSWORD = $null
```

The seed creates:

- the menu (6 module types, 34 modules, including Stations, Sales Report, Batch Print, Statement of Accounts and Custom Lab Form) and the 15 services the lab forms hang off, at **price 0** (the lab sets prices on the Services screen; the two unbuilt "Medical Examination" services are inactive);
- the reference ranges for Hematology, Clinical Chemistry, Physical Examination and Clinical Microscopy (**the lab should confirm these**);
- the Patients dropdowns (Gender, Civil Status);
- the `Admin` user with the temporary password, which must be changed at first sign-in.

It does not create companies, departments, items, discounts, packages, signatory names or the Stool/Fecalysis defaults: those are entered in the app. It refuses to run on a database that already has users, modules or services. Administrators see every screen, so no permission rows are needed.

After editing a module or a reference range, regenerate it with `tools\Generate-Seed.ps1 -Server <server>` (reads a reference database, writes `db/seed.sql`).

Rollback of a failed install is simply dropping the database and starting again.

## Upgrading an existing database

Releases after the first install add migrations (for example Batch Print, and Statement of Accounts with its two tables). For a database that is already in use:

1. Back up, then run `db\schema.sql` on it: it is idempotent and applies only the missing migrations. Run it **before** stations start the new program; until then the version check keeps stations on the new program out with "ask your administrator to update the database".
2. Migrations may change permissions on existing users (the Statement of Accounts migration gives users who could print it Create and Delete as well). Read the migration's comment first and tell the lab's administrator.
3. New screens that only some users should see (Batch Print, Custom Lab Form) need the permission granted in Users; the seeded Admin sees everything. Each form a lab makes in Custom Lab Form is a screen of its own and needs its own permission too. Those forms live in the database (tables `CustomForms*`), so they are in the backups and are not part of `db/seed.sql`.
4. **Saved Statements of Accounts are records the lab hands to companies:** they are copies that do not change when payments change, so the backups of this database are the only way to reproduce them. Keep the backup routine from this document running and tested.

## Migrating the client's legacy data (one time, on the server)

The client entered real services with prices, packages and companies in the legacy build. `db\migrate-from-legacy.sql` copies all of it into an **empty** V2 database in one transaction (any error rolls everything back; the legacy database is only read). It also adds the menu entries the legacy build never had (modules 29 to 34) and gives administrators full permissions. Dry-run on the client's 2026-10-17 backup: every row count and payment/amount sum matched; only Modules (+6), Services (+3, priced 0), ModuleDefaults and UserPermissions differ, as expected.

Do it before any station is used, once. In an elevated PowerShell on the server, in the kit folder (needs the **current** `db\schema.sql`, `db\migrate-from-legacy.sql`, `db\verify-migration.sql` and `Install-Server.ps1` with `-SkipSeed`: rebuild the kit with `Make-ServerKit.ps1` and carry it over). SQL instance below is `.\SQLEXPRESS`.

1. **Find the legacy data.** `sqlcmd -S .\SQLEXPRESS -E -C -Q "SELECT name FROM sys.databases"`. If `DiagnosticLabsDB` is there, that is the client's data. Close the legacy app everywhere. If it is not there, restore the newest client backup (`db\DiagnosticLabs10172026.bak` is from 2026-10-17 and misses anything entered since: ask for a fresh one).
2. **Work on a copy.** Back up the legacy database, restore it as `DiagnosticLabsDB_Client` (`RESTORE ... WITH MOVE` to this instance's data folder), and add the column the older build lacks: `ALTER TABLE Payments ADD IsCharge bit NOT NULL CONSTRAINT DF_Payments_IsCharge DEFAULT 0;` (skip if it exists). The original is never touched.
3. **Back up V2 as it is** (`Backup-Database.ps1`), and confirm nothing real was entered in it: only the seed's Admin and menu.
4. **Rebuild V2 empty.** With everyone out of the app: `ALTER DATABASE DiagnosticLabsV2 SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE DiagnosticLabsV2;` then `.\Install-Server.ps1 -SkipNetwork -SkipShare -SkipBackups -SkipSeed`. It creates the database with `schema.sql` only and re-grants `dl_app` (the login and the share's `config` file stay as they are).
5. **Migrate.** Make a wrapper file (sqlcmd `-v` rejects values with spaces) `run-migrate.sql`:
   ```
   :setvar Legacy DiagnosticLabsDB_Client
   :setvar LegacyTimeZone "Singapore Standard Time"
   :r db\migrate-from-legacy.sql
   ```
   then `sqlcmd -S .\SQLEXPRESS -d DiagnosticLabsV2 -E -C -I -b -i run-migrate.sql`. It ends with "Migration committed" after a list of skipped rows (should be empty).
6. **Verify.** `sqlcmd -S .\SQLEXPRESS -d DiagnosticLabsV2 -E -C -I -b -i db\verify-migration.sql -v Legacy=DiagnosticLabsDB_Client`: everything `ok` except the expected differences above. Take a backup of the migrated V2.
7. **Sign in** with the legacy administrator account (legacy passwords keep working and are re-hashed at first sign-in; the seed's temporary Admin no longer exists). Set the prices of Physical Examination, Clinical Microscopy and Physical Examination (Walk-In) (new services, price 0), grant the new screens to the users who need them, and check Home, a package, a company and a printout.
8. **Then release the app** (`Update-Server.ps1`, see below) and bring the stations on.

Going back is safe at any point before step 8: drop V2 and repeat from step 4. The legacy database and the `_Client` copy are never changed by the migration.

If the generator changes (a new module or migration after this document), regenerate the script with `tools\Generate-LegacyMigration.ps1 -Server <dev server>` and dry-run it again on a scratch database first.

## Releasing an update (after go-live)

Two scripts do the whole server side; stations update themselves on their next launch.

**On the build PC**

1. Tests green. If a migration was added: regenerate `db/schema.sql` and `db/seed.sql`, and test `schema.sql` on a scratch database.
2. `.\tools\deploy\Publish-Release.ps1 -Version 1.0.N -Zip` builds `artifacts\release-1.0.N.zip`. The zip also carries `_server\schema.sql` (never copied to the share), and the script refuses to pack if `schema.sql` is older than the newest migration. Use a plain `1.0.N` number each release: the server refuses a version that is not newer.
3. Carry the zip to the server (AnyDesk file transfer).

**On the server, elevated PowerShell, outside lab hours**

```powershell
.\Update-Server.ps1 -Release D:\Drop\release-1.0.N.zip -BackupDir D:\Backups -CopyDir E:\LabBackups
```

(`-BackupDir` is remembered in `update-state.json` after the first run.) It checks the release, takes and verifies a backup (stops if that fails), runs `schema.sql`, moves the share's `current` to `previous`, mirrors the new release into `current` (`version.txt` removed first and written last), checks the result, and writes `update-state.json` and `update.log` in the share root (`C:\DiagnosticLabs`). Open the app once on the server PC to confirm, then it reaches the stations as they are launched. A station that stays open all day updates the next morning.

**What the stations show**

- **New-version notice:** an open app looks at the share's `version.txt` every 10 minutes. When it differs from the version the program was built as, a yellow bar under the header says so, with **Restart now** (asks about unsaved changes first, closes the app, and the launcher reopens it on the new version a few seconds later) and **Later** (hides it for that release only). The launcher passes the share path and its own location to the app through the environment variables `Update__SharePath` and `Update__Launcher`, so there is nothing to configure per station; an app started any other way (a developer build) never shows the notice.
- **Stations list** (Settings, administrators only; module 34, read-only): every PC that has signed in, its version, last user and last-seen time, with "Behind" in red for any PC not on the share's version. The app writes its own row (table `StationVersions`, one row per computer name) at sign-in and every 10 minutes while open; "Open now" means seen in the last 25 minutes. If the share cannot be read from the server PC, the newest version any station reported is used as the yardstick.
- The release number is the build's `-Version` (`Publish-Release.ps1 -Version 1.0.N`), so keep it identical to what you give `Update-Server.ps1`; a plain `dotnet build` shows `1.0.0`.

**Going back:** `.\Rollback-Server.ps1` puts `previous` back on the share. If the update applied migrations, the old program refuses to run on the newer database, so the script stops and asks for `-RestoreDatabase`, which restores the pre-update backup after a safety backup of the current data. **Everything entered since the update is lost**, so everyone must be out of the app, and the script wants the database name typed to confirm. Often the better answer to a bad release is a fixed release (1.0.N+1) rather than a rollback.

Rules that keep this safe:

- Make migrations additive (new columns and tables, renames done in two releases) wherever possible, so the previous program can still run if you must go back.
- Batch fixes into a few releases a week, not one per fix; note what changed for the lab.
- Not tested on the real server yet: the backup step inside `Update-Server.ps1` and the `-RestoreDatabase` path. The rest (checks, schema.sql on an empty database, share swap, version order, rollback refusals, app-only rollback) ran against a scratch database and folder.

## Next server update (after 1.0.2): notes

Pending for the next release (1.0.3 or later). Two migrations change the client's live data, so read this before running `Update-Server.ps1`.

**What changes for the lab**

1. **Annual Physical Exam Page 2 is part of the Annual Physical Exam** (migration `MergeAnnualPhysicalExamPage2`). The Annual Physical Exam screen has two tabs (Annual Physical Exam, Page 2) and one Defaults button; Print always gives both pages. "Annual Physical Exam Page 2" disappears from the menu, from Home and from the services.
2. **Medical Examination Report is renamed "Physical Examination (Walk-In)"** in the menu, on the screen and as a service (migration `RenameMedicalExamReportToWalkIn`). The printout keeps its title, MEDICAL EXAMINATION REPORT.

**What the update does to existing data** (all inside `schema.sql`, in one go)

- A saved Page 2 result joins the Annual PE of the same registration (its Remarks, contact number and civil status fill in what the Annual PE does not have; the emptied Page 2 record is soft-deleted). A Page 2 with no Annual PE to join becomes an Annual PE with an empty first page.
- The **Page 2 service is deleted.** A registration line for it becomes an Annual PE line at the same price. A registration that had **both** keeps one Annual PE line priced at the two together, so its total and amount due do not change. In a package, the Page 2 line becomes an Annual PE line, or is removed if the package already has the Annual PE (a package's price is its own, so it does not change).
- Page 2's entry lists and saved defaults move to the Annual PE (where both had a default for the same field, the Annual PE's is kept).
- The Walk-In service is renamed only if it is still called "Medical Examination Report" (a name the lab gave it is kept).

**Before the update** (on the server, read only; save the output with the update log):

```sql
-- what the merge will touch
SELECT COUNT(*) AS Page2Results FROM LabReports WHERE ReportType = N'MedicalExamination' AND IsDeleted = 0;
SELECT s.Id, s.ServiceName, s.Price FROM Services s JOIN Modules m ON m.ServiceId = s.Id WHERE m.Id IN (16, 17, 33);
SELECT r.RegistrationCode, rs.Price, CASE WHEN EXISTS (SELECT 1 FROM PatientRegistrationServices o WHERE o.PatientRegistrationId = r.Id AND o.ServiceId = (SELECT ServiceId FROM Modules WHERE Id = 16) AND o.IsDeleted = 0) THEN 'also has APE' END AS Note
FROM PatientRegistrationServices rs JOIN PatientRegistrations r ON r.Id = rs.PatientRegistrationId
WHERE rs.ServiceId = (SELECT ServiceId FROM Modules WHERE Id = 17) AND rs.IsDeleted = 0;
SELECT p.PackageName FROM PackageServices ps JOIN Packages p ON p.Id = ps.PackageId WHERE ps.ServiceId = (SELECT ServiceId FROM Modules WHERE Id = 17);
```

If the lab gave the Page 2 service a price, ask them what the Annual Physical Exam should cost now that it includes Page 2 (registrations already made keep what they were billed).

**Release:** the usual steps above (`Publish-Release.ps1 -Version 1.0.N -Zip`, then `Update-Server.ps1`). `db/schema.sql`, `db/seed.sql` and `db/migrate-from-legacy.sql` are already regenerated with both migrations.

**After the update**

- Sign in and open Annual Physical Exam: both tabs, and Print gives two pages. Open one of the registrations listed above and check its total.
- **Services:** set the Annual Physical Exam price if it changes (see above).
- **Users:** anyone who had only "Annual Physical Exam Page 2" needs "Annual Physical Exam" (the old permission row stays but no longer opens anything).
- **Defaults:** an administrator opens Annual Physical Exam → Defaults once and checks both tabs.
- Tell the lab: Page 2 is now a tab; "Medical Examination Report" is now "Physical Examination (Walk-In)" in the menu (it sits next to "Physical Examination", the Laboratory Results sheet, so point out which is which).

**Going back:** these migrations are not additive (a service is deleted and results are merged), and the 1.0.2 program refuses the newer database, so a rollback needs `Rollback-Server.ps1 -RestoreDatabase` (the pre-update backup; anything entered since is lost). Prefer a fixed release.

## Server install script

`tools/deploy/Install-Server.ps1` sets up the server in one pass and is safe to run again; `tools/deploy/Make-ServerKit.ps1` packs it with the db scripts into `artifacts/server-kit.zip` (copy that to the server by USB).

1. On the server, in an **elevated** PowerShell, as the Windows account that installed SQL Server: `.\Install-Server.ps1 -CheckOnly`. It reports Windows, the SQL instance (edition, version, sysadmin, login mode), TCP/IP and port, firewall, network profile, sleep, and whether the database exists, and changes nothing.
2. Read the report, then run it for real: `.\Install-Server.ps1 -BackupDir D:\Backups -CopyDir E:\LabBackups -RegisterBackupTasks` (add `-SqlServer ".\NAME"` if there are several instances, `-SetPrivateNetwork` if a network is Public, `-ServerAddress <fixed IP>` if station PCs cannot resolve the server name). It will ask for the **temporary Admin password** (or take it from `DL_ADMIN_PASSWORD`).
3. It then: turns TCP/IP on with the fixed port 1433 and enables mixed-mode logins (restarting the SQL service only if something changed), opens the firewall for the local subnet, switches off sleep and hibernate; creates the database and runs `schema.sql` and `seed.sql` when it is empty (an existing database is left alone); creates the `dl_app` login (random password, read/write on this database only); creates the share `\SERVER\DiagnosticLabs` with a read-only Windows account `dlupdate` and writes `config\appsettings.Local.json`; gives the SQL service rights on the backup folder, takes and verifies a first backup, and (with `-RegisterBackupTasks`) schedules the backups.
4. At the end it prints the **`dlupdate` password once**. Store it: each station needs it (`cmdkey /add:SERVER /user:SERVER\dlupdate /pass:...`) because a workgroup PC cannot read a share without an account.
5. Then get the release onto the share. `Publish-Release.ps1` runs on the **build PC** (it needs the repo and the .NET SDK), not on the server. Build and pack it there with `.\tools\deploy\Publish-Release.ps1 -Version 1.0.0 -Zip` (makes `artifacts\release-1.0.0.zip`, about 68 MB), carry the zip to the server (USB or the remote session), unzip it, `robocopy <unzipped> C:\DiagnosticLabs\current /MIR /XF version.txt`, then copy `version.txt` **last**. With a network path from the build PC you can instead use `-ShareTarget \\SERVER\DiagnosticLabs\current` (it needs the server administrator login; `dlupdate` is read-only). Then install the first station.

Tested on a scratch database: create, schema, seed, login, and a second run that changes nothing. **Not tested yet** (they change Windows, so they were not run on the development PC): the network, firewall, power, share and Windows-account steps, the backup-folder permissions and the scheduled tasks. Treat the first run on the real server as the test of those, and read the `-CheckOnly` report before it.

## First real install (2026-10-17): what happened and what to remember

The server is a Windows 11 Pro PC (build 26200) with **SQL Server 2025 Express** (17.0, instance `.\SQLEXPRESS`), a private network, and the kit in `D:\DiagnosticLabs\V2\`. EF Core 10 and the SQL login work against SQL Server 2025 with `TrustServerCertificate=True`. The first `-CheckOnly` found TCP/IP off, no firewall rule and the database missing; the real run fixed all of that and everything after it succeeded.

Things that went wrong or nearly did, now fixed or written down:

- **`New-LocalUser -Description` is limited to 48 characters.** The script's description was 50, so the run stopped at step 5 before creating the `dlupdate` account. Fixed in `Install-Server.ps1`; the script is safe to run again, which is how it was finished (the second run left the database alone, reset the `dl_app` password because no config file existed yet, and wrote the config).
- **`Publish-Release.ps1` was tried on the server.** It only works on the build PC. The release now travels as a zip (`-Zip`).
- **Script execution policy.** Run `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass` once in the window (it lasts for that window only) or start scripts with `powershell -ExecutionPolicy Bypass -File ...`. Scripts also need `.\` in front when started from their own folder.
- **The temporary Admin password was kept in a Notepad file on the server.** It is temporary (forced change at first sign-in), but use something stronger than a guessable word and delete the note once signed in.
- **The computer name is the Windows default** (`DESKTOP-U6KN9AF`) and every station stores it. Rename it, or use a fixed IP with `-ServerAddress`, **before** configuring stations.
- **A database backup landed inside the repository (`db\*.bak`).** Backups hold patient data and must never be committed; `*.bak` is now in `.gitignore`. Keep backups on the backup disk only.
- **Not yet done:** the stations (the next session), the backup restore test, and the real-printer checks in `docs/REHEARSAL.md`.