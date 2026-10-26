# Architecture
_Last updated: 2026-10-05 · Scope: layering and the Excel coordinate layer flow. See `CODE_MAP.md` for file-level paths._

## Layers
`Domain` (models, rules, geometry; no dependencies) ← `Application` (interfaces, GeoJSON builder) ← `Infrastructure` (SQL Server, MBTiles, Excel, JSON store) ← `Wpf` (views, view models, WebView2 host + `Assets/Map` page).
Read-only SQL rules (`ReadOnlySqlGuard`, typed parameters) are untouched by the coordinate layers: they never touch the database.

## Excel coordinate layer flow
**Excel Import → Layer Model → Map Renderer → Radius Visualization**

| Step | What happens | Where |
|---|---|---|
| 1. Excel Import | User picks `.xlsx/.xls`; first sheet, columns Name/Latitude/Longitude, WGS84 degrees. Bad rows are skipped and reported in Persian. | `MapViewModel.ImportExcelCommand` → `IExcelImportService` → `ExcelCoordinateImportService` |
| 2. Layer Model | A valid `MapLayer` (points, `Visibility`, `RadiusMeters`) is saved as JSON and shown in the panel. | `Domain/CoordinateLayers.cs`, `ICoordinateLayerStore`, `CoordinateLayerItem` |
| 3. Map Renderer | The view sends GeoJSON (points + circles + bounds) to the page; the page caches it and draws on top of the opened map. | `CoordinateLayerGeoJson` → `MapView` (`coordUpsert`) → `Assets/Map/coordinate-layers.js` |
| 4. Radius Visualization | Radius text → metres (`RadiusInput`) → a ring of real WGS84 vertices (`GeoMath.CircleRing`) → MapLibre projects it to Web Mercator as fill + outline. | `GeoMath`, layers `coord|<id>|fill/line` |

### CRS and radius (the important decision)
- Stored and imported coordinates are WGS84 (EPSG:4326, degrees). The map renders Web Mercator (EPSG:3857).
- A radius is **never** sent as a number to the page and never drawn as a pixel circle. A circle of fixed metres is a different number of degrees (longitude) and pixels at every latitude, so it is built per point as a polygon (64 segments) with the spherical destination formula, radius 6,371,008.8 m (same value as the MapLibre scale bar).
- Metres are only parsed at one place (`MapViewModel.ApplyRadiusAsync` via `RadiusInput`); model, store and geometry are metres only.

### Page contract (host → page messages)
`coordUpsert {id,name,visible,radiusMeters,points,circles,bounds}` · `coordVisible {id,visible}` · `coordRemove {id}` · `coordFit {id}`.
The page keeps the last `coordUpsert` per id and redraws after every map (re)load, so opening another map file keeps the layers. `map.html` only has three additive hooks (script tag, `coordAttach` in `load()`, `coordHandle` in the message handler).

### Controls
Show/hide → `Visibility` saved; Refresh → reload the layer from the store and redraw (the stored copy wins); Zoom to extent → `fitBounds` over points + circles (single point without radius: zoom ≥ 14); Remove → store + map. Panel is beside the map, never over it (WebView2 airspace).

### Out of scope (by design)
Spatial database search, clustering, large-layer optimisation, column mapping, DMS.
