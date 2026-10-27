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


## P6 — visual design system and home dashboard (branch `feature/complete-ui-redesign`)
The first visual-polish slice preserves the existing MVVM command and navigation bindings and changes presentation only:
- Refined light/dark palette tokens and reduced default card shadow weight.
- Improved common visual rhythm (title, button minimum height, data-grid row height, card geometry).
- Rebuilt the connected Home dashboard around connection status, quick routes to Explorer/Map/Monitoring, informational metrics and recent alerts.
- Increased shell breathing room slightly while retaining the existing five-section navigation, active profile/database selectors, Ctrl+K command palette and persistent map host.

Verification status: not compiled or run by the AI. Static binding review only so far; owner must run `dotnet build FastDbExplorer.slnx`, `dotnet test`, and manually inspect light/dark themes, RTL, home shortcuts, reconnect/disconnect and alert updates on Windows.


## P7 — connection, Explorer and monitoring visual pass
Presentation-only refinements on the core workflows:
- Connection profiles have clearer profile identity, profile count, and a contained credential-storage note. The connection form has a clearer title block; bindings and password-handling flows are retained.
- Explorer's table list has a compact count indicator and stronger visual separation between table identity and approximate row count. Query execution remains in the existing TableQueryView/ViewModel path.
- Monitoring's header uses the available grid space instead of keeping an empty first column; spacing is tuned for the toolbar and alerts panel. All commands, item templates, record previews, report links, and monitor state bindings remain in place.

Verification: XML tag-stack check and static ShellStrings key checks passed for the changed XAML. No .NET build or test has run in this environment; Windows build and end-to-end verification remain required.


## P8 — monitor editor and map toolbar refinement
- Grouped primary monitor identity/source/watermark settings into one clear card, with a localized section title and supporting hint.
- Grouped schedule interval, report output folder, and sound preference into a separate card so users can scan the setup in a predictable order.
- Added the supporting Persian strings in `MonitoringStrings.cs`.
- Refined map toolbar breathing room and visual hierarchy only; map commands, context-menu handlers, visibility triggers, WebView2 host, and side-panel structure remain unchanged.

Verification: balanced XAML tag checks and presence checks for all `MonitoringStrings` references passed for the edited editor and map view source. This does not replace a WPF compile. Build, automated tests, and manual map/monitor editing tests are still required on Windows.
