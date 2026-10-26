# Features
_Last updated: 2026-10-05. Status: ✅ done & owner-verified · 🟡 delivered, not verified by AI · 🧱 foundation only (no UI) · ⏳ planned_

## SQL Server explorer
| Feature | Status |
|---|---|
| Connect (Windows / SQL auth), database list, table list + search, recent connections (no passwords) | ✅ MVP-01 |
| Column picker, 13-operator parameterised filters, hybrid OFFSET→keyset paging, cancel, timeout | 🟡 MVP-02 (owner reports it works) |
| Virtualised viewer, header sorting, column management | ⏳ MVP-03 |

## Map viewer
| Feature | Status |
|---|---|
| MBTiles vector + raster (MapLibre in WebView2), layers panel, cursor coordinates, zoom, fit | 🟡 |
| GMDB (GMap.NET raster cache), labels with offline fonts | 🟡 |
| OSM PBF → MBTiles converter | ⏳ |

## Excel coordinate layers
| Feature | Status | Notes |
|---|---|---|
| Import `.xlsx` / `.xls` with columns Name, Latitude, Longitude (WGS84 decimal degrees) | 🟡 Phase 2 | button "وارد کردن Excel" on the map toolbar; invalid rows skipped, summary in Persian |
| Models `MapPoint`, `MapLayer` (Visibility, RadiusMeters in metres) | 🟡 Phase 1 | validated in the constructor |
| Layers survive restart (JSON per layer in `%AppData%\FastDbExplorer\coordinate-layers`) | 🟡 Phase 2 | loaded when the map page opens |
| Each point drawn as marker (dot + name label) | 🟡 Phase 2 | on top of the opened map file |
| Radius circle per point, radius typed in metres, metre-accurate on the map | 🟡 Phase 2 | geodesic polygon, not a pixel circle; 0 = no circle |
| Show/hide, refresh (reload from store), zoom to extent, remove | 🟡 Phase 2 | per-layer panel beside the map |
| Works without an opened map file | ⏳ later | needs a blank-basemap mode |
| Rename layer, multiple worksheets, `.xlsm` | ⏳ later | |
| Clustering, large-layer performance | ⏳ later | explicitly out of Phase 2 |
| Column detection / mapping, DDM / DMS conversion | ⏳ later | |
| Database spatial queries against layers | ⏳ later | explicitly out of Phase 2 |
