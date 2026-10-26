# Map screen redesign + marker icons

> Branch: `feature/saved-connection-credentials` (work in progress, **not compiled or run by the AI**; the owner builds and tests).

## What changed
**Layout (MapView.xaml)**
- One slim toolbar: title + file name on one side; on the other two quiet icon buttons (fit to map, show/hide panel, drawn as vector shapes), a separator, "open map" and the primary "add Excel layer" button.
- One side panel (350 px, beside the map, never over it because of WebView2 airspace) with two tabs: **Excel points** and **map layers**, replacing the two always-open panels.
- Excel layer card is collapsed by default: colour/icon badge, name, "N points · radius", zoom, visibility check, and a chevron that opens the settings (marker icon, colour, radius, rename, refresh, delete).
- Map layers tab: show all / hide all and the checkbox list. Empty states for "no Excel layers yet" and "this map has no layers".

**Marker icons**
- 24 in-house icons (`Domain/MapMarkerCatalog.cs`, 24x24 grid, SVG path data, no third-party licence) + 10 colours. Picked per Excel layer; the choice is saved in the layer file (`iconKey`, `color`, both optional, old files load unchanged; an unknown icon or invalid colour is dropped).
- A new layer gets the next palette colour automatically; layers without a choice keep the classic dot marker.
- On the map the icon is drawn on a canvas (disc + white glyph) and shown as a MapLibre symbol layer; an invisible circle layer underneath keeps hover/click working.
- A style change re-sends the layer (`coordUpsert`); the page rebuilds only that layer.

**Password eye (Connections screen)**: now vector shapes (eye, plus a slash while visible) instead of font glyphs, which showed a square on some systems.

## Files
New: `Domain/MapMarkerCatalog.cs`, `Wpf/Services/MapIconGeometry.cs`, `Wpf/Localization/MapUiStrings.cs`, `Wpf/ViewModels/LayerStyleChoices.cs`, `tests/.../MapMarkerCatalogTests.cs`, this doc.
Changed: `Domain/CoordinateLayers.cs` (`IconKey`, `Color`), `Infrastructure/.../JsonCoordinateLayerStore.cs`, `Wpf/ViewModels/CoordinateLayerItem.cs`, `MapViewModel.cs`, `Wpf/Views/MapView.xaml` + `.cs`, `Wpf/Assets/Map/coordinate-layers.js`, `Wpf/Views/ConnectionsView.xaml`.

## Manual test checklist
1. Build + `dotnet test` (new `MapMarkerCatalogTests`).
2. Connections: the eye shows an eye; click -> eye with a slash and the password is visible; click again -> hidden. No square.
3. Map: open a map; one panel with two tabs; hide/show it with the toolbar button; fit button works.
4. Add an Excel layer: it appears in the Excel tab with a colour badge; the points on the map use that colour.
5. Open the card (chevron): pick an icon -> points on the map turn into that icon at once; pick a colour -> colour changes on map + badge + radius circles.
6. Close and reopen the app: icon and colour are still there.
7. Hover/click an icon marker: tooltip, status-bar text and the selection ring still work.
8. Visibility check, zoom, radius, rename, refresh, delete (with confirmation) still work.
9. Map layers tab: show all / hide all; a raster map shows the "no layers" text.
10. Dark and light theme, RTL; zoom in/out (icon size changes smoothly).

## Known risks / not verified
- Whole change uncompiled. Likely trouble spots: XAML resource names (`IconButton`, `SegmentButton`, `PrimaryButton`, brushes), `Path` data parsing of the icon glyphs in WPF, `Path2D`/`addImage` on the bundled MapLibre version.
- Icon glyphs are simple on purpose; if some look wrong tell me which and I redraw them.
- Query-result points (from the explorer) keep the default marker.
