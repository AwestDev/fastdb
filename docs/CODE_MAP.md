# Code map (start here)
**Connect flow:** `ConnectionView.xaml` -> `ConnectionViewModel.ConnectAsync` -> `IDatabaseMetadataService.GetDatabasesAsync` (`SqlServerMetadataService`) -> `IConnectionProfileStore.SaveAsync` -> event `Connected` -> `MainViewModel.OnConnected` -> `ExplorerViewModel`.
**Tables flow:** pick database in `ExplorerView.xaml` -> `ExplorerViewModel.LoadTablesAsync` -> `GetTablesAsync` (reads `sys.tables`/`sys.partitions`, no table scan).
**Read-only safety:** every query passes `ReadOnlySqlGuard.EnsureReadOnly`; connection string uses `ApplicationIntent=ReadOnly`.
**Theme:** `ThemeService` swaps `Themes/Light.xaml` <-> `Dark.xaml` (colours); `Themes/Styles.xaml` holds all control styles.
**Texts:** all in `Localization/Strings.cs` (coordinate-layer texts: `Localization/CoordinateStrings.cs`; splash: `Localization/SplashStrings.cs`). **Saved connections:** `%AppData%\FastDbExplorer\connections.json` (no passwords).
**Errors:** `SqlException` -> `DatabaseAccessException(kind)` in Infrastructure -> Persian message via `Strings.Describe`.
**Start-up:** `App.OnStartup` -> `SplashWindow` (`Report(step, total, text)`) -> build DI host -> `_host.StartAsync` -> `ThemeService.Start` -> create `MainWindow` -> show it -> close splash (min 1.2 s, `MinSplashTime`). Start-up failure: splash closes, message box, `Shutdown(1)`.

## MVP-02 (query engine)
**Run flow:** `TableQueryView.xaml` -> `TableQueryViewModel.RunAsync` -> `PageRequest` -> `SqlServerMetadataService.GetPageAsync` -> `GetColumnsAsync` (validates names) -> `SelectQueryBuilder.Build` (SQL + typed parameters) -> `ReadOnlySqlGuard` -> reader (page size + 1 rows).
**Paging:** `TableQueryViewModel` keeps a stack of `PageCursor(offset, afterKey)`. OFFSET below 10,000 rows, keyset (`key > last key`) from there on; Previous pops the stack. Keyset needs the primary key (unique); tables without one use the user-chosen order column + OFFSET only.
**Typed values:** `ValueConverter` (text -> int/date/guid/...); LIKE wildcards escaped in `SelectQueryBuilder`.
**Grid columns** are built at run time in `TableQueryView.xaml.cs`; rows are `GridRow` (pre-formatted strings, bound by index).

## Map module
`MapView.xaml(.cs)` (WebView2 host, tile interception) · `MapViewModel` · `MapSourceFactory` → `MbTilesMapSource` / `MvtLayerReader` · `Assets/Map/map.html` (MapLibre page + generic style) · `FileDialogService`. Details: PROJECT_MASTER_CONTEXT §5 and §9.

## Excel coordinate layers (Phase 1 foundation + Phase 2 map connection)
**Import:** `MapViewModel.ImportExcelCommand` -> `IFileDialogService.PickExcelFile` -> `IExcelImportService` -> `ExcelCoordinateImportService.Import` (ExcelDataReader, first sheet, header Name/Latitude/Longitude) -> `CoordinateImportResult` (`MapLayer` or `ImportIssue` codes -> Persian via `CoordinateStrings.Describe`).
**Persistence:** `ICoordinateLayerStore` -> `JsonCoordinateLayerStore` -> `%AppData%\FastDbExplorer\coordinate-layers\{id}.json`. Loaded once by `MapViewModel.EnsureCoordinateLayersLoadedAsync` (called from `MapView.Loaded`).
**Models/rules:** `Domain/CoordinateLayers.cs` (`MapPoint`, `MapLayer`, `CoordinateRules`). **Geometry:** `Domain/GeoMath.cs` (`CircleRing`, `LayerBounds`, `RadiusInput`).
**Render flow:** `CoordinateLayerItem` (panel row, commands) -> `MapViewModel` events (`CoordinateLayerChanged/VisibilityChanged/Removed/ZoomRequested`) -> `MapView` `coord*` messages (data from `CoordinateLayerGeoJson`) -> `Assets/Map/coordinate-layers.js` (cache + MapLibre layers `coord|<id>|fill/line/dot/label`).
**Tests:** `CoordinateLayerTests.cs` (Phase 1), `CoordinateRenderingTests.cs` (Phase 2). Status and limits: `CURRENT_STATE.md`; design: `ARCHITECTURE.md`; graph: `ai/PROJECT_GRAPH.md`.
