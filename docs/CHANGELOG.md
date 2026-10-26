# Changelog
Format: newest first. Dates are ISO. Everything below is unverified by a build until the owner confirms (see `CURRENT_STATE.md`).

## [Unreleased] — 2026-10-16 — Detachable map window and categorized marker icons (branch `feature/saved-connection-credentials`)
### Added
- "باز کردن در پنجره جدا" button on the map toolbar: the map opens in its own `MapWindow` (for a second monitor). It shares the same `MapViewModel` (layers, file, camera); the Map section shows a note with "بازگرداندن به برنامه" while it is detached. The WebView is collapsed in the section while detached (native window, no WPF layering).
- Marker icon library grouped in 11 folders (عمومی، درمانی، آموزشی، مذهبی، حمل‌ونقل، دولتی، امنیتی و نظامی، انرژی و صنعت، طبیعت و زیرساخت، تجاری و خدمات، فرهنگی و تفریحی): 56 icons, up from 24. Icons drawn in-house as SVG path data, as before.
- `MapMarkerCategory` in the domain catalog; `MapMarkerCategoryTests.cs` (every icon in exactly one folder, the 24 earlier keys still exist, at least 50 icons).

### Changed
- The layer's icon picker is one button that shows the current icon; the folders and their icons open in a popup (no longer a long flat grid in the side panel).

### Not verified
Not compiled or run: the build machine for this change had no .NET SDK. XAML bindings (`IsPoppedOut`, `ToggleWindowCommand`, `IconCategories`, `DisplayIconShape`) and the new icon paths still need an owner build and a visual check of the icon sheet.

## [Unreleased] — 2026-10-06 — Navigation shell and UI overhaul (branch `feature/nav-shell`)
### Added
- Persistent navigation rail (Home, Explorer, Map, Monitoring) with Ctrl+1..4; sections keep their state, no Back buttons.
- Shared top bar: connection status, active-database picker, disconnect, search button.
- Home dashboard when connected (connection summary, section tiles, latest alerts); connect form is the Home page when disconnected; Explorer/Monitoring show a "connect first" invitation.
- Unread-alert badge on the Monitoring item.
- Ctrl+K palette (`CommandPaletteWindow`): sections, tables of the active database, monitors, disconnect.
- `ShellStrings.cs`, `NavItemViewModel`, `HomeViewModel`, `ConnectPromptViewModel`, `CommandPaletteViewModel`, `HomeView`, `ConnectPromptView`.

### Changed
- `MainViewModel` is now the shell (sections instead of one swapped `CurrentView`); `MainWindow.xaml` hosts rail + top bar.
- Explorer sidebar 340 -> 300 px, tables only (database picker moved to the top bar).
- Connect screen: brand text describes the whole app; map entry card removed (map is in the rail); tagline updated.
- Map/Monitoring "Back" buttons removed (script step).

### Not included
Map layers in the Ctrl+K palette; Ctrl+1..4/Ctrl+K do not fire while the WebView2 map has keyboard focus. See `docs/NAV_REDESIGN.md`.
## [Unreleased] — 2026-10-06 — Start-up splash screen (branch `feature/monitoring-alerts`)
### Added
- `Views/SplashWindow.xaml(.cs)`: borderless, transparent, rounded 760x460 window (fixed dark design): radial glow, faint contour lines, three expanding ripples, database logo with shadow, name + Persian tagline, progress bar with shimmer, status line `text  n / 3`, footer (read-only note + version from the assembly).
- `Localization/SplashStrings.cs`: tagline, footer and the three step texts.

### Changed
- `App.xaml.cs`: `OnStartup` shows the splash first and reports the real steps (1 services/DI, 2 host start, 3 theme + main window creation). The main window is shown before the splash closes; the splash stays at least 1.2 s (`MinSplashTime`). A failure during start-up closes the splash, shows the message and shuts the app down (before, the async-void exception left the app half started). DI registrations and their order are unchanged.

### Not included (by design)
No images/fonts downloaded from the web (the app uses its embedded Vazirmatn); no per-theme variant; the bar steps are start-up stages, not a percentage of real work.

## [Unreleased] — 2026-10-05 — Excel Coordinate Layer Import, Phase 1 (branch `feature/excel-coordinate-layers-phase1`)
### Added
- `MapPoint`, `MapLayer`, `CoordinateRules`, `ImportIssue` / `ImportIssueCode`, `CoordinateImportResult` (Domain).
- `IExcelImportService`, `ICoordinateLayerStore` (Application).
- `ExcelCoordinateImportService`: `.xlsx`/`.xls`, required columns Name/Latitude/Longitude, WGS84 validation, row-level and file-level errors with codes, streaming read, cancellation.
- `JsonCoordinateLayerStore`: one JSON file per layer, atomic write, tolerant load.
- Tests: `CoordinateLayerTests.cs` (import, model rules, persistence) with a built-in minimal `.xlsx` writer.
- Docs: `CURRENT_STATE.md`, `FEATURES.md`, `CHANGELOG.md`; notes in `CODE_MAP.md` and `DECISIONS.md` (D20–D26).

### Changed
- `FastDbExplorer.Infrastructure.csproj`: new package `ExcelDataReader 3.*`.
- `App.xaml.cs`: registers the two new services in DI (no behaviour change).

### Not included (by design)
Map rendering, UI panel, column detection, DDM/DMS conversion, database spatial queries.

### Remaining for Phase 2
1. `IFileDialogService.PickExcelFile` + a view model that calls `IExcelImportService`, shows the result, and saves with `ICoordinateLayerStore`.
2. Persian texts for every `ImportIssueCode` in `Strings.cs` (same pattern as `Strings.Describe` for database errors); show row/column.
3. Load saved layers at startup (`LoadAllAsync`) and expose them as an observable collection; also surface skipped/damaged layer files.
4. Layers panel: visibility toggle, radius input (convert user units to metres at the edge), rename, delete; call `SaveAsync` on change.
5. Map rendering: send layers to the WebView2 page (`layer`-style host↔page messages), draw points and metre-accurate radius circles (geodesic polygon or zoom-scaled circles), fit-to-layer. Remember WebView2 airspace (panels beside the map only).
6. Performance plan for large layers (clustering / GeoJSON size), and a cheaper save for visibility/radius changes.
7. Decide open questions: multiple worksheets, `.xlsm`, rows without a name, accurate Excel row labels.
8. Pin the `ExcelDataReader` version after the first restore; add a manual-test checklist for a real `.xls`.

## Earlier work
See `PROGRESS.md` and `PROJECT_MASTER_CONTEXT.md` §10 (MVP-01, MVP-02, Map slices 1–2).
