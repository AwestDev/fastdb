# Monitoring (پایش و هشدار) — branch `feature/monitoring-alerts`

Status: **written, NOT compiled or run by the AI** (no .NET in the AI sandbox). Build + test on Windows first (`dotnet build FastDbExplorer.slnx`, `dotnet test`).

## What it does (phases 1–3 of the plan)
1. **Dynamic conditions** – the user picks database, table, the *watermark* column and any number of condition rows (same 13 operators / typed parameters as the table filters). Column names are never hard-coded: every table can call location / origin / destination differently.
2. **Scheduling** – each monitor has its own interval (1–1440 min). An in-process scheduler (`MonitorScheduler`) ticks every 15 s and runs due monitors one at a time. Runs only while the app is open and connected (password stays in memory).
3. **Alerts + Excel** – when new records match, a corner message appears, an optional sound plays, and an `.xlsx` with the matching records is written to the chosen folder. Alert history is shown in the monitoring screen (open report / show in folder).

## How "new since last check" works
- The user chooses a *watermark column*: increasing number (`int/bigint/smallint/tinyint`) or date (`date/datetime/datetime2/smalldatetime`), e.g. an identity PK or CreatedAt/UpdatedAt.
- Each check: `SELECT MAX(wm), SYSDATETIME()` (one seek if indexed) → `upper`; query only `wm > last AND wm <= upper` plus the user's conditions, server side, ordered by `wm, PK`, OFFSET pages of 1000, cap 10,000 rows per check; then `last = upper`.
- **First check only records the starting point** (no alerts for old data). Table empty at that time → everything that appears later counts as new.
- If a check hits the 10,000-row cap, the watermark moves to the last row seen and the rest is picked up next time (rows sharing exactly the same watermark value at the cap boundary could be skipped; unlikely with ids / datetimes).
- Updated records are only detected when the watermark column changes on update (e.g. `UpdatedAt`).

### Late-committed rows (fixed in step 3)
A row with a slightly older date/id can be committed *after* a newer one (parallel transactions). If the window ended exactly at MAX it would be skipped forever.
- **Date columns:** `upper = min(MAX, SQL-server-time − SettleSeconds)`, default 30 s (`MonitorDefinition.SettleSeconds`, stored in `monitors.json`; no screen field yet). This can only *delay* a row, never lose it: an idle table is still reported once 30 s have passed; a column stored in another time zone just waits longer.
- **Number (identity) columns:** no clock exists, so no protection. Prefer a date column when many transactions insert in parallel (the editor hint says so).

### Failed checks (fixed in step 3)
A failed check (server down, column dropped, folder not writable, timeout) now shows a corner message + an entry marked "⚠" in the alert list on the **first** failure of a streak and again every 10th failure. A successful check resets the streak. The monitor keeps retrying at its interval.

### Editor race (fixed in step 3)
Saving an edit keeps the newest check state (`LastWatermark`, `LastCheckedUtc`), so a check that finished while the editor was open cannot be undone (no duplicate alerts).

## Safety rules kept
- Read-only: only `SELECT MAX(...), SYSDATETIME()` and the normal parameterized page query, both through `ReadOnlySqlGuard`. Nothing is written to the monitored database.
- State lives in `%AppData%\FastDbExplorer\monitors.json` (definitions + last watermark; no passwords).
- No new NuGet package: the `.xlsx` is written by `XlsxReportWriter` (System.IO.Compression + XmlWriter). Columns with unsupported types (blobs, xml) are not read.

## Files
- Domain: `MonitoringModels.cs` (`MonitorDefinition`, `MonitorCheckResult`, `WatermarkReading`, `WatermarkTypes`); `PageRequest.RequiredFilters` (AND-ed with user filters).
- Application: `Abstractions/MonitoringAbstractions.cs`.
- Infrastructure/Monitoring: `MonitorCheckService` (incl. `SettleUpper`), `MonitorScheduler`, `SqlServerWatermarkReader`, `XlsxReportWriter`, `JsonMonitorStore`, `WatermarkComparer`.
- Wpf: `MonitoringView`, `MonitorEditorView`, `AlertToastWindow`, `MonitoringViewModel`, `MonitorEditorViewModel`, `MonitorItemViewModel`, `MonitoringViewModelFactory`, `Services/AlertNotifier|FolderPickerService|ShellOpen`, `Localization/MonitoringStrings`.
- Tests: `tests/FastDbExplorer.Tests/MonitoringTests.cs` (query builder, comparer, scheduler due-logic, xlsx, store, baseline, new rows, nothing-new, row cap, empty-table baseline, settle).

## Work plan (owner approves each step before the next)
1. **Owner:** build + test on Windows (`dotnet build`, `dotnet test`), send errors. *(AI cannot run .NET.)*
2. **Owner:** manual end-to-end test on a real table (baseline → insert matching rows → toast, sound, xlsx → restart app, watermark kept).
3. **Done (this commit):** late-committed rows (date settle), failed-check alerts, editor race.
4. **Next:** docs for the other files (CURRENT_STATE, FEATURES, CHANGELOG, CODE_MAP, DECISIONS, PROJECT_MASTER_CONTEXT).
5. Later: persistent alert history, index warning for MAX on big tables, settle seconds field in the editor, history sheet in reports (source/destination per record), per-monitor report columns, background/Windows-service mode, sound file choice, remembered "start from" value.

## Other known limits
- `SELECT MAX(col)` on a column without an index scans the whole table (30 s timeout on huge tables). Use an indexed column (primary key / indexed created-at). No warning in the editor yet.
- Alert history is memory only (max 100); the Excel files stay on disk.
- `GetColumnsAsync` is called a few times per check (small metadata queries).

## Risks to check on Windows
- Segoe glyph `&#xE823;` (clock) on the explorer sidebar and `&#xE7BA;` in the toast must render on Windows 10.
- `OpenFolderDialog` (.NET 8+ WPF) and `PeriodicTimer(TimeSpan, TimeProvider)` are used.
- Open the generated `.xlsx` in Excel once (Persian text, frozen header, right-to-left sheet).
