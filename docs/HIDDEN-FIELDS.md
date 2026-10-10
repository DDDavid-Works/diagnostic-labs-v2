# Hidden fields

Fields the client's forms no longer show or print, that are **kept in the database** so nothing already saved is lost.
They are not on the screen, not on the printout and not in the search; saving a result does not touch them (there are tests that check this).
Do not drop one until the client has confirmed that nothing in it is needed. Add a row here whenever a field is hidden.

| Where | Field | Form | Hidden since | What it held and what happened to it |
|---|---|---|---|---|
| `StoolFecalysisReports` | `Result` | Fecalysis | Fecalysis rework (`FecalysisRework`) | The single free-text Result of the old form. Its text was copied into the result's Remarks (or into Others when the Remarks already had text); the column itself was left as it was. |
| `LookupValues` (module 5, field "Result") | the saved Result texts | Fecalysis | Fecalysis rework | The reusable texts of the old Result box. Still in the table, no longer offered by any screen. |
| `ModuleDefaults` (module 5) | the saved default for "Result" | Fecalysis | Fecalysis rework | An administrator's saved default for the old Result box, if there was one. The screen ignores a saved key it does not know. |
| `HematologyReports` | `StabNValue`, `StabResult` | Hematology | Hematology rework (`HematologyRework`) | The Stab line (normal value and result). The client's new form has no Stab line. Old results keep what they had; the new form cannot show or change it. |

## Dropping a hidden field later

1. Ask the client to confirm that nothing in it is needed (print a few old results to compare if in doubt).
2. Remove the property from the entity and its configuration, add a migration that drops the column, and update `db/schema.sql`.
3. Remove its row from this list.
