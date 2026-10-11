---
name: render-check
description: Run the offscreen UI check for DiagnosticLabs (opens the real WPF screens against a scratch database, fills them, saves, reopens, prints, and writes screenshots and printout PNGs). Use after any screen, printout or view model change, and when you must SEE a form (layout, clipping, overlap) before reporting it done.
---

# Offscreen UI check (tools/UiHarness)

A small WPF host that creates the real windows with the real services against a **scratch database** (`DL_UiCheck`, created and dropped by the runner), drives the view models, and saves PNGs. It is how forms and printouts get looked at without a person clicking.

## Run it
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\UiHarness\Run-Check.ps1 -Mode <mode> [-StartAt <migration to apply first>]
```
- `-Mode` picks the check (the runner passes `<Out>\<mode>` and the harness chooses by the END of that folder name; see the dispatch lines near the top of `tools/UiHarness/Program.cs`): `physical`, `microscopy`, `fecal`, `renames`, `dirty`, `home`, ...
- `-StartAt <previous migration>` applies migrations up to that one, copies the lab modules from `DiagnosticLabsV2` (read only), then applies the rest, so a migration that seeds a module/service/defaults is exercised for real.
- Output: `[ok]`/`FAIL` lines, binding-error count, and the PNGs in `$env:TEMP\ui-check\<mode>` (screens `*.png`, printouts `print-<Layout>.png`). The exit code is the number of failed checks.
- `DL_NO_ERROR_DIALOG=1` is set by the runner: without it the app's global error box pops up on the user's desktop. Never run the harness by hand without it.

## Always LOOK at the images
Open the PNGs with Read. Check: long values and a long company name, empty form, scrolled bottom of tall forms (the harness scrolls with `ScrollViewer.ScrollToVerticalOffset/ScrollToEnd`), the logo not overlapping text, clipped labels, lines doubled up, text growing upward instead of downward.

## Add a check for a new or changed form
Create `tools/UiHarness/<Name>.cs` (copy `Microscopy.cs`): `Open<TViewModel>(ModuleIds.X)`, set fields inside `Act(() => {...}, 800)` (setting view model properties outside `Act` runs off the dispatcher and can log a "CollectionView" error), `Check(name, condition, detail)`, `Snapshot("x.png")`, save, `NewRecordCommand`, `ToggleSearchCommand` + `SearchCommand`, pick `Items.First()`, reopen and compare, `PrintCommand` (the harness captures the printout). Register the view model in the `AddTransient` list and add a dispatch line (`if (_out.EndsWith("<mode>")) {...}`) in `Program.cs`.

## Notes
- It builds into `%TEMP%\uiharness-out` because a running DiagnosticLabs app locks `src/DiagnosticLabs.Wpf/bin`. After changing app code the runner rebuilds the harness, so the PNGs are never stale (a stale build was once mistaken for an unfixed bug).
- About a dozen `ComboBoxItem` binding warnings appear when a check selects items in a combo box directly; they are test noise, not app errors. Any other binding error is real.
- Older modes (`ape`, `registration`, `payments`, ...) may need small updates after field renames.
- It never touches `DiagnosticLabsV2` or the legacy database, except the read-only module copy of `-StartAt`. Do not point it at them.
