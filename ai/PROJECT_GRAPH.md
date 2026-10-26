# Project graph (AI orientation)
_Last updated: 2026-10-05. Read `CLAUDE.md` first, then `docs/ARCHITECTURE.md`._

## Flow: Excel Import → Layer Model → Map Renderer → Radius Visualization
```mermaid
flowchart LR
  A[Excel file .xlsx/.xls] -->|IFileDialogService.PickExcelFile| B[MapViewModel.ImportExcelCommand]
  B -->|IExcelImportService| C[ExcelCoordinateImportService]
  C --> D[(MapLayer: MapPoint[], Visibility, RadiusMeters)]
  D -->|ICoordinateLayerStore| E[(coordinate-layers/id.json)]
  D --> F[CoordinateLayerItem panel]
  F -->|RadiusInput meters| G[GeoMath.CircleRing WGS84 polygon]
  D --> H[CoordinateLayerGeoJson points/circles/bounds]
  G --> H
  H -->|coordUpsert via MapView| I[coordinate-layers.js]
  I --> J[MapLibre: dot + label + circle fill/line]
```

## Nodes
| Node | File | Role |
|---|---|---|
| Models / rules | `src/FastDbExplorer.Domain/CoordinateLayers.cs` | `MapPoint`, `MapLayer`, `CoordinateRules` (Phase 1) |
| Geometry | `src/FastDbExplorer.Domain/GeoMath.cs` | `CircleRing`, `LayerBounds`, `RadiusInput` |
| GeoJSON | `src/FastDbExplorer.Application/CoordinateLayers/CoordinateLayerGeoJson.cs` | page data, pure and tested |
| Import / store | `src/FastDbExplorer.Infrastructure/CoordinateLayers/` | Excel reader, JSON store (Phase 1) |
| View model | `src/FastDbExplorer.Wpf/ViewModels/MapViewModel.cs`, `CoordinateLayerItem.cs` | import, list, controls, events |
| View | `src/FastDbExplorer.Wpf/Views/MapView.xaml(.cs)` | panel + `coord*` messages |
| Page | `src/FastDbExplorer.Wpf/Assets/Map/coordinate-layers.js`, `map.html` | drawing, cache, zoom |
| Texts | `src/FastDbExplorer.Wpf/Localization/CoordinateStrings.cs` | Persian UI text and issue messages |
| Tests | `tests/FastDbExplorer.Tests/CoordinateRenderingTests.cs` | geometry, parsing, GeoJSON |

## Rules to keep
Existing map features (MBTiles, GMDB, labels) are not changed; coordinate layers are additive. Radius = metres everywhere except the text box. No spatial SQL, clustering or large-layer tuning yet.
