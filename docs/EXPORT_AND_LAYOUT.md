# Explorer layout, smart export (Excel / split CSV), «مانیتورینگ»

> Branch: `feature/map-tools-and-markers`. **Written, NOT compiled or run by the AI.**

## 1. The result table got its room back
The table was squeezed by a settings card (260 px), an "Excel export" row (50 px) and the tall map panel (150 px). Now:
- **Map panel = one compact row** (title, two column pickers, details button; selection chip + the one button on the other side). It wraps on a narrow window. "Why is the button off?" is a tooltip; a line appears under the row only when rows ARE selected but cannot be sent. The per-point details open as an extra row on demand (the button shows "· 2/8").
- **"Excel export" moved into the header**, next to Run (no row of its own).
- **The settings card hides itself after the first result** (the gear button in the header brings it back). The header shows "· N فیلتر فعال" in the accent colour, so it is clear that filters are still applied.
- Slightly smaller gaps around the results card.

## 2. Smart export: Excel or CSV, split when big
One rule in `XlsxReportWriter` (used by the Explorer export and by monitoring reports):

| Result size | What is written |
|---|---|
| up to about **2,000,000 cells** (e.g. 100,000 rows × 20 columns); never above 1,048,575 rows | one real **.xlsx** (typed cells, phone numbers stay text) |
| bigger | **CSV** (UTF-8 with BOM, comma, header repeated), a new file every **500,000 rows or 100 MB** |

- CSV parts are named `name_part001.csv`, `name_part002.csv` …; if everything fits in one part it is just `name.csv`. Existing files are never overwritten (a time stamp is added instead). Cancel or an error deletes every file written so far.
- The writer reads up to the limit first (bounded memory), then streams the rest; the decision needs no extra query.
- After a CSV export the message says that the result was big, how many rows/files, the folder, and how to open it in Excel without losing leading zeros of phone numbers (**Data ← From Text/CSV**, column type Text). Double-clicking a CSV in Excel turns `09123456789` into `9123456789`; that is how Excel reads CSV, not a bug of the file.
- Limits are in `ExportLimits` (defaults above); tests use tiny limits.

## 3. «پایش» → «مانیتورینگ»
The section is now "مانیتورینگ" everywhere in the texts (navigation, home, search palette, the screen title "مانیتورینگ و هشدار"), and one item is a "مانیتور" ("مانیتور جدید", "ویرایش مانیتور", "نام مانیتور"). Changed files: `ShellStrings.cs`, `MonitoringStrings.cs`. The map result chips already read the section name from `ShellStrings`, so they follow. If you still see «پایش» somewhere, tell me where (a hard-coded text in a XAML file would be the only remaining place).

## Files
New: `Application/Abstractions/ExportAbstractions.cs` (`ExportLimits`, `QueryExportResult`), `Infrastructure/Monitoring/CsvPartsWriter.cs`, `Wpf/Localization/ExportStrings.cs`, `tests/.../QueryExportTests.cs`.
Changed: `MonitoringAbstractions.cs` (`IQueryReportWriter.WriteQueryAsync` now returns `QueryExportResult`), `XlsxReportWriter.cs`, `TableQueryViewModel.cs`, `TableQueryView.xaml`, `MapSendPanel.xaml(.cs)`, `MapSendStrings.cs`, `ShellStrings.cs`, `MonitoringStrings.cs`.

## Manual test checklist
1. Build + `dotnet test` (new `QueryExportTests`).
2. Explorer: run a query. The settings card closes by itself, the table is much taller, the header shows the active-filter count. The gear opens the card again.
3. Map panel is one row; select 4 rows: the chip and the button text update; with nothing selected the button is off and its tooltip explains; the details button opens/closes the extra row.
4. Export a small result: a normal .xlsx, same message as before.
5. Export a big result (for example a table with several hundred thousand rows): CSV file(s) next to the chosen name, the message names the count and folder; open one with Excel via Data ← From Text/CSV.
6. Cancel a big export: no partial files remain.
7. The navigation item and every screen of the section say «مانیتورینگ».

## Not changed
- Dates: the Explorer grid shows Persian-calendar dates, the Monitoring preview ISO dates (asked before; waiting for your decision).
- The CSV separator is a comma. If your Windows regional list separator is different, double-click may put everything in one column; Data ← From Text/CSV always works.
