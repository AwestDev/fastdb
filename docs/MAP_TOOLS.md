# Map tools: coordinates, distance, my points, result sets

> Branch: `feature/map-tools-and-markers` (from `feature/geosent-source-upload` @ `c667e69`). The first version built and passed all 194 tests on the owner's machine; the second round (below) is **written, NOT compiled or run by the AI**.

## What the user gets
All tools live **on the map itself** (a small HTML overlay in the map page), so they also work in the detached map window.

**Top-left toolbar:** a coordinates box + three icon buttons.
1. **Go to coordinates:** type `35.7219, 51.3347` (also `35.7219 51.3347`, `35.72N 51.33E`, `35°43'19"N 51°20'05"E`, Persian digits) and press Enter. The map flies there, drops a red temporary pin and shows a card with latitude/longitude. Wrong input shows a short red message.
2. **Get coordinates:** turn the crosshair on and click anywhere (or simply **right-click** the map at any time). A card shows latitude and longitude with a copy button for each and for "lat, lng".
3. **Measure distance** (like Google Maps): click points; a live total follows the mouse, every vertex shows its running distance, the card has undo / finish / clear. Double-click, Enter or right-click finishes; Backspace undoes; Esc leaves the tool. Distances are great-circle (haversine).

**Card actions:** *Add to my points* (optional name) saves the point permanently in the layer **«نقاط من»** (pin marker); *Measure from here*. Clicking a saved "my points" point shows a card with copy and **delete**.

## Round 2 (owner feedback)
- **Right-to-left text:** every number in the cards is an isolated left-to-right run inside a right-to-left line, so it reads "32.22 کیلومتر" / "300 متر" (number first, then the unit) and "2 نقطه". The coordinates box is right-to-left while empty (Persian hint) and left-to-right while typing digits. Rule for the whole app: put numbers in an isolated LTR run; never rely on the surrounding direction.
- **Detached map window:** the window is now left-to-right, so the minimize / maximize / close buttons are at the top right like the main window; its content stays right-to-left.
- **Speed:** the shell creates the map view once (first visit) and afterwards only hides/shows it, so returning to the Map section no longer rebuilds the WebView2 + page + tiles. A view released by the pop-out is replaced by a fresh one. A theme change while the map was hidden is applied when it is shown again. Palette actions that wait for the page run immediately when the page is already ready.
- **Result sets:** every send from the Explorer or Monitoring is a **new set** and nothing is replaced automatically. Explorer and Monitoring sets coexist. Each set has its own chip over the map (zoom / remove) and "remove all" appears when there are two or more. The very same rows from the same table are not added twice (the existing set is only shown). Disconnecting still removes all sets (the rows belong to that session).
- **Look per source:** Excel layers = **round** (own icon and colour), my points = **pin**, Explorer = **amber diamond with a magnifier**, Monitoring = **rose rounded square with a bell**.

## Files
`Domain/MapSourceStyles.cs` (kinds, shapes, fixed colours/glyphs), `map-tools.js`, `coordinate-layers.js`, `map.html`, `MapView.xaml.cs` + `MapView.Tools.cs`, `MapViewModel.cs` / `.Tools.cs` / `.Actions.cs`, `MapToolStrings.cs`, `MainViewModel.cs` (passes the source), `MainWindow.xaml(.cs)` (persistent map host), `MapWindow.xaml(.cs)`, tests `MapSourceStylesTests`.

## Manual test checklist
1. Build + `dotnet test`.
2. Distance card: the total reads "32.22 کیلومتر" (number on the right, unit on its left) and the line below "2 نقطه · آخرین قطعه: ..."; the empty coordinates box shows the hint aligned right.
3. Pop the map out: minimize / maximize / close are at the top right; the map UI is still right-to-left; closing returns to the Map section.
4. Open the map, go to another section and back several times: the map is there at once (no blank wait); the camera is unchanged. Open the map, switch the Windows theme, come back: the map follows the theme.
5. Explorer: send rows -> amber diamonds + chip; go back to the Explorer, send other rows -> a second chip, both sets stay. Send the same rows again -> no duplicate.
6. Monitoring: send rows -> rose squares with a bell + chip; the Explorer set is still there. Chips: zoom, remove one, "remove all".
7. Disconnect: all result sets disappear.
8. Everything from the first round still works (go to, pick, measure, my points, hover/click on all marker shapes).
9. Ctrl+K palette -> a map layer: zooms even though the map was already open.

## Known risks / not verified
- Uncompiled. The persistent map host is the riskiest part: a WebView2 inside a collapsed container, and replacing a released view after the pop-out. If the map ever stays blank after a return, tell me what you did before.
- Result sets are never saved; only "my points" are persistent.
