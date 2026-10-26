# Navigation & UI overhaul (FastDbExplorer)

Status: P1-P5 written (UNVERIFIED: not compiled, the AI sandbox has no .NET). Owner must build on Windows and test.

## Problem (from code)
- `MainViewModel.CurrentView` swapped whole screens; Map and Monitoring were reachable only through pills inside the Explorer sidebar (or a link on the connection screen) and needed Back buttons.
- The 340 px Explorer sidebar held connection status, database list, table list and the pills.
- The first screen was the connection form with a "read-only explorer" brand; the app now has Map + Monitoring + coordinate layers.

## Decisions
1. Connection is a STATE, not the first page. Disconnected: Home shows the connect form (existing ConnectionView), Explorer/Monitoring show an invitation to connect, Map always works. Connected: Home is a dashboard.
2. Persistent navigation rail (right side in RTL): Home, Explorer, Map, Monitoring. Section ViewModels keep their state (no Back buttons).
3. Shared top bar: connection status, active-database picker, disconnect.
4. After a successful connect the app goes to Home.
5. Name stays FastDbExplorer (`WindowStrings.AppName`).

## Phases
- P1 Shell (done, unverified): `MainViewModel` sections, `NavItemViewModel`, rail in `MainWindow.xaml`, `ConnectPromptView`.
- P2 Home + top bar (done, unverified): `HomeViewModel/HomeView`, top bar, unread-alert badge on the Monitoring item (counts alerts added while another section is open).
- P3 Lighter Explorer (done, unverified): sidebar 340 -> 300 px, tables only; DB picker moved to the top bar.
- P4 (done, unverified): Ctrl+1..4 and Ctrl+K palette (`CommandPaletteViewModel`, `CommandPaletteWindow`; searches sections, tables of the active DB, monitors, disconnect). TODO: map layers in the palette.
- P5 (done, unverified): connect screen brand text for the whole app, map entry card removed, tagline, CHANGELOG entry. TODO later: empty/error-state polish, merge "recent connections" into Home, update CODE_MAP/PROGRESS.

## Known limits
- Ctrl+1..4 do not fire while the map (WebView2 native window) has keyboard focus.
- Views are recreated when switching sections (as before); ViewModels keep the state. The map reloads its last file as before.
