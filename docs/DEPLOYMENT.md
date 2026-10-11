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
- **Create the database:** run `db/schema.sql` on an empty `DiagnosticLabsV2`. Note that it creates tables only: it has no starting data (users, menu/modules, permissions, services, company), so nobody can sign in until that exists. See "Starting data" below.
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

### Database version check (not built yet)

There is no version table in `db/schema.sql` yet. Suggested:

- A `SchemaVersion` table, updated by numbered migration scripts (`db/migrations/001-...sql`).
- The app compares the version it was built for with the database at start-up and shows a clear "update required" message instead of failing later with a SQL error.
- Release order: back up, run migrations, publish the new app folder. Stations on the old version are told to restart and pick up the new one.

## Rollout order

1. **Rehearsal on a spare PC:** install SQL Server Express, run `schema.sql`, publish the app, run a station through the launcher.
2. **Pilot:** server plus 2 Windows 10/11 stations for about a week, with staff entering real-looking test registrations. Printing is the thing to check: preview, printer choice and PDF export on the actual lab printers (US Letter).
3. **Go-live:** clear the pilot test data (or restore the clean starting-data backup), then bring stations on in batches. There is no legacy data to migrate or freeze.
4. **Retire the old database** on the client PC: take a last backup of `DiagnosticLabsDB`, keep it somewhere safe for a while, then drop it.

## Open items

- Count and spec the Windows 7 PCs (CPU 64-bit, RAM, disk) to see how many can be upgraded rather than replaced.
- Decide the starting data (see below) and which stations are needed on day one.
- Confirm the backup target (second disk, NAS or external drive) and who rotates it.
- Decide where `appsettings.Local.json` comes from for a new station (a template file in the update share with the password filled in on site).
- Build: `SchemaVersion` table and start-up check. (Publish profile, launcher and backup scripts are done, see "Tools" below.)
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

`db/schema.sql` creates empty tables, and the app has no built-in seeding. A new database therefore has no users, no menu (`Modules`), no permissions, no services and no company details, and nobody can sign in.

The legacy never went live, so there is no history to migrate, but the old database on the client's PC may hold **setup the client already entered** (services and prices, company details, users, discounts). Options:

| Option | Notes |
|---|---|
| **A. Seed from the old database** | Use `db/migrate-from-legacy.sql` once on the empty V2 database, then delete any test patients, registrations and lab rows. Keeps whatever setup the client already did and the menu/permission rows. Needs a look at what the old database actually holds first. |
| **B. A proper seed script** | Write `db/seed.sql` (menu, permissions, an admin user, empty lists) and let the client enter services and company data. Cleaner for a first install and repeatable for future sites. More work now. |

Recommended: A to get going (it is built and verified), B if this app will be installed at other labs.
