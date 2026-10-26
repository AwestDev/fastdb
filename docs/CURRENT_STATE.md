# Current state
_Last updated: 2026-10-05 · Branch: `feature/excel-coordinate-layers-phase1` (Phase 2 files are layered on top) · Phase: Excel Coordinate Layers, **Phase 2 (map connection)**_

## Verification status (read this first)
Nothing here has been **compiled or run** by the AI (its sandbox has no .NET). The JS page code was syntax-checked and smoke-tested with node against a fake map object, and the XAML is well-formed XML; everything else is unverified until the owner runs `dotnet build FastDbExplorer.slnx` and `dotnet test` on Windows and checks the map by hand (see *Test procedure*).

## What exists after Phase 2
Flow: **Excel Import → Layer Model → Map Renderer → Radius Visualization** (details in `ARCHITECTURE.md`).

| Piece | Where |
|---|---|
| Models, rules (Phase 1) | `src/FastDbExplorer.Domain/CoordinateLayers.cs` |
| Geometry: `GeoMath.CircleRing`, `LayerBounds`, `RadiusInput` | `src/FastDbExplorer.Domain/GeoMath.cs` |
| GeoJSON for the page | `src/FastDbExplorer.Application/CoordinateLayers/CoordinateLayerGeoJson.cs` |
| Excel import + JSON store (Phase 1) | `src/FastDbExplorer.Infrastructure/CoordinateLayers/` |
| Import command, layer list, visibility, radius, refresh, zoom, remove | `ViewModels/MapViewModel.cs`, `ViewModels/CoordinateLayerItem.cs` |
| Panel + messages to the page | `Views/MapView.xaml`, `Views/MapView.xaml.cs` |
| Drawing on the map | `Assets/Map/coordinate-layers.js` (+3 additive hooks in `map.html`) |
| Excel file dialog | `Services/FileDialogService.cs` (`PickExcelFile`) |
| Persian texts | `Localization/CoordinateStrings.cs` (separate file so `Strings.cs` stays untouched) |
| Tests | `tests/FastDbExplorer.Tests/CoordinateRenderingTests.cs` (+ Phase 1 `CoordinateLayerTests.cs`) |

### Behaviour
- Import button on the map toolbar (visible when a map is open). A file-level problem shows a Persian message and no layer; bad rows are skipped and counted.
- Radius: typed in **metres**, applied with the button or Enter. Invalid text (negative, comma, above 20,037,508) shows an error and changes nothing. Persian digits and `٫` are accepted. 0 = no circle.
- Circles are polygons of real WGS84 vertices (64 segments, sphere radius 6,371,008.8 m = MapLibre scale bar value), so they keep their real size at any latitude/zoom.
- Visibility and radius changes are saved (`SaveAsync`, serialized through one gate). Refresh re-reads the layer from the store and redraws; if the store no longer has it, the current copy is redrawn and a message is shown.
- Saved layers load once when the map page first appears and are re-sent every time the page is created. Opening another map file keeps the layers (the page caches and redraws them).

### Not changed
SQL Server explorer, MBTiles/GMDB rendering, labels, themes, existing `Strings.cs`, existing tile layers panel. `map.html` has only three additive lines/blocks; `MapViewModel` got a longer constructor (DI resolves it) and `ShowCoordinatePanel`.

### Not included (by design)
Spatial database search, clustering, large-layer optimisation, rename, column mapping, DMS.

## Test procedure
1. `dotnet build FastDbExplorer.slnx` then `dotnet test` (new: `CoordinateRenderingTests`).
2. Run `setup-map-assets.cmd` if not done, start the app, open any map file (MBTiles/GMDB).
3. Click "وارد کردن Excel", pick a file with Name/Latitude/Longitude: the panel shows the layer, markers appear.
4. Type a radius (e.g. 1000) and press Enter: circles appear. Compare one circle's width with the scale bar at zoom ~14 (should be about 2 km across, at any latitude).
5. Uncheck the layer: markers and circles disappear; check again: they return. Restart the app: the layer, its visibility and radius are back.
6. Zoom button: map fits all points and circles. Refresh button: layer redraws, message appears. Remove: layer disappears from map and panel.
7. Open a different map file: the layers are still drawn.
8. Try a file with a bad row, a wrong header and a `.csv` renamed to `.xlsx`: Persian messages, no crash.

## Known issues / limits
1. **Not compiled** (see top). Likely trouble spots: `CommunityToolkit.Mvvm` generated command names (`ZoomCommand`, `RefreshCommand`, `RemoveCommand`, `ApplyRadiusCommand`), the `DangerBrush` resource used for error text (missing key only logs a binding warning), Segoe MDL2 glyphs `E8A3/E72C/E74D`.
2. Coordinate layers need an opened map file (the map page and WebView only exist then). A blank-basemap mode is future work.
3. Circles that cross the antimeridian keep unwrapped longitudes (>180); circles that contain a pole are not handled; radii near the maximum shrink because the sphere is used.
4. Labels need the offline `NotoSans` font files from `setup-map-assets.cmd`; without them markers still show but labels do not.
5. Spherical distances: below 0.5 % difference from the WGS84 ellipsoid.
6. A saved layer file is rewritten completely on every change (Phase 1 limit); damaged layer files are still skipped silently by the store.
7. Refresh cannot re-import the Excel file (only the file name is stored, not its path); it reloads from the store.
8. Phase 1 limits remain: `ExcelDataReader 3.*` unpinned and needing owner approval (D20), `.xls` not covered by an automated test, first worksheet only.
