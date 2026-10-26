# Shared "show on the map" panel (Explorer + Monitoring)

> Branch: `feature/map-tools-and-markers`. **Written, NOT compiled or run by the AI.**

## Why
The Explorer results and the Monitoring result preview do the same thing (pick the latitude/longitude columns, select rows, send them to the map) but looked and behaved differently:

| | Explorer (before) | Monitoring (before) |
|---|---|---|
| Column labels | "عرض جغرافیایی" | "ستون عرض جغرافیایی" |
| Button text | "نمایش ردیف‌های انتخاب‌شده روی نقشه" | "ارسال ردیف‌های انتخاب‌شده به نقشه" |
| Button position | above the table, beside the columns | below the table, bottom corner |
| Details of a point | collapsed expander | always visible |
| Selected count | shown | not shown |
| Button disabled | no reason given | no reason given |

## What it is now
One control, `Views/MapSendPanel` (+ `MapSendStrings`), used in both places, directly above the table:
1. **Title + selection chip:** globe badge, "نمایش روی نقشه", and "N ردیف انتخاب‌شده" (or a muted "ردیفی انتخاب نشده").
2. **Two column pickers** ("ستون عرض جغرافیایی", "ستون طول جغرافیایی") with their labels above them.
3. **One primary button** that says what it will do: "ارسال به نقشه" / "ارسال 4 ردیف به نقشه". It is on only when rows are selected, both columns are chosen and the selection holds valid coordinates.
4. **A hint under it when it is off:** no rows selected (how to select several), no columns chosen, or no valid coordinates in the selection.
5. **Details of each point** in a collapsed expander ("· 2/8", with a one-line explanation) in both screens.

Each screen just binds its own members (columns, latitude/longitude, detail columns, selected count, send command); the rules and the wording live in the control, so they cannot drift apart again.

## Files
New: `Views/MapSendPanel.xaml(.cs)`, `Localization/MapSendStrings.cs`, `ViewModels/MonitoringViewModel.MapSend.cs` (selected-row count for Monitoring).
Changed: `Views/TableQueryView.xaml` (Explorer), `Views/MonitoringView.xaml` and `.xaml.cs`. The Explorer keeps its own "Excel export" button above the panel (a different action: all rows).

## Manual test checklist
1. Build + `dotnet test`.
2. Explorer: run a query on a table with coordinates. The panel is above the grid; nothing selected: muted "ردیفی انتخاب نشده", the button is off and the hint explains how to select rows.
3. Select 4 rows: chip shows "4 ردیف انتخاب‌شده", the button reads "ارسال 4 ردیف به نقشه" and is on; click: the Map section opens with the diamonds.
4. Clear one coordinate column (or pick a column without coordinates): the hint changes.
5. Open the details expander: the checkboxes work and the counter "· n/8" follows; more than 8 is refused as before.
6. Monitoring: open a check result ("نمایش نتایج"). The SAME panel, same texts, same behaviour; sending shows the rose squares with a bell.
7. Close the preview and open another one: the selection count starts from zero.

## Observations (not changed)
- The Explorer grid shows dates in the Persian calendar with Persian digits, the Monitoring preview shows ISO Gregorian dates. If you want one format everywhere, tell me which.
- "Excel export" exists in the Explorer as a button and in Monitoring as report links on alerts; they are different features, so they stay separate.
