# FastDbExplorer — Project Master Context & AI Handoff (v MVP-02 + Map slice 1 + adaptive paging)

> **Purpose:** if the chat is lost, this single file + the repository is enough for a new AI agent or developer to continue.
> **Last updated:** 2026-10-08 · **Owner language:** Persian (UI is Persian RTL) · **Docs language:** English (AI-friendly)

## 0. خلاصه به زبان ساده (Persian summary)
- برنامه‌ی دسکتاپ **فقط‌خواندنی** برای کاوش پایگاه‌داده‌های خیلی بزرگ SQL Server (WPF، .NET 10، رابط فارسی راست‌چین، تم تیره/روشن خودکار).
- **MVP-01 انجام و توسط مالک تأیید شده:** اتصال (Windows/SQL)، لیست دیتابیس‌ها و جداول، جستجوی جدول، اتصال‌های اخیر (بدون رمز).
- **MVP-02 نوشته و تحویل شده؛ مالک گفته خوب کار می‌کند (جزئیات تست نشده):** انتخاب ستون، فیلتر پارامتری با ۱۳ عملگر، صفحه‌بندی ترکیبی OFFSET→Keyset، لغو، timeout.
- **صفحه‌بندی تطبیقی:** نسخه SQL Server و سطح سازگاری فقط‌خواندنی بررسی می‌شود؛ در صورت نیاز کوئری `ROW_NUMBER()` جایگزین می‌شود و هیچ تنظیم یا ساختار دیتابیسی تغییر نمی‌کند.
- **فیلتر تاریخ شمسی و خروجی نتایج (کدنویسی‌شده، هنوز build/run نشده):** ورودی تاریخ شمسی برای Explorer/Monitoring، نمایش شمسی در grid، انتخاب ردیف و لایه‌ی موقت نقشه، و خروجی Excel صفحه‌به‌صفحه.
- **ماژول نقشه (مرحله‌ی ۱ نوشته شد، هنوز تست نشده):** نمایش MBTiles (وکتور و تصویری) با MapLibre داخل WebView2، لایه‌ها، مختصات، زوم. GMDB فقط اگر ساختار MBTiles داشته باشد باز می‌شود (آزمایشی) و در غیر این صورت فهرست جدول‌ها گزارش می‌شود. PBF از نوع OSM خام است (`asia-latest.osm.pbf`) و باید ابتدا به MBTiles تبدیل شود؛ تبدیل‌گر مرحله‌ی بعد است. **برای GMDB واقعی به یک نمونه‌ی کوچک فایل نیاز داریم.**
- محدودیت مهم AI: محیط AI لینوکس است، .NET و اینترنت ندارد ⇒ AI **نمی‌تواند build/اجرا کند**. هر خروجی AI تا وقتی مالک روی ویندوز build و تست نکرده «تأییدنشده» است.

## 1. Product
**FastDbExplorer** — professional read-only desktop explorer for very large, sensitive SQL Server databases (hundreds of millions of rows, ~300k new rows/day).
Goal: read/analyze safely with maximum performance and minimum load. Never modify data.

## 2. Hard rules (never break)
1. **Read-only.** No INSERT/UPDATE/DELETE/CREATE/ALTER/DROP/TRUNCATE/MERGE/EXEC. Enforced by: `ApplicationIntent=ReadOnly` in the connection string, and `ReadOnlySqlGuard` on every command (single SELECT/WITH only; ignores [identifiers] and 'literals').
2. **Filtering happens on the server.** Never load whole tables. Never `SELECT *`. Never `DataTable`. Stream with `SqlDataReader`; always page; always support cancellation; command timeout 30 s.
3. **User values are never concatenated into SQL.** Only typed parameters (`ValueConverter`). Identifiers come from the real column list and are bracket-quoted.
4. **No database or app-login passwords on disk.** Named connection profiles store their friendly name, stable ID, server, auth mode, username, and certificate setting only; legacy recent-connection JSON is migrated on load. SQL passwords are requested for each SQL-auth connection and kept in memory for that session. The startup `admin` / `123` login is a demo-only UI gate, not real security and does not protect database data; see `docs/DEMO_LOGIN.md`.
5. **No `COUNT(*)`** on big tables. Approximate rows come from `sys.partitions`; "has next page" = fetch pageSize+1.
6. Small vertical slices. Verify before claiming done. Do not redesign architecture without technical evidence.

## 3. Stack
.NET 10 · WPF · MVVM (CommunityToolkit.Mvvm 8.*) · Microsoft.Data.SqlClient 6.* · Microsoft.Extensions.Hosting 10.* (DI) · xunit 2.* (tests) · Microsoft.Data.Sqlite 10.* (map files) · Microsoft.Web.WebView2 1.* + MapLibre GL JS 5.x (map rendering, downloaded by `setup-map-assets.cmd`). NuGet versions are **floating** — pin after first successful restore.
Solution file is `FastDbExplorer.slnx` (needs .NET 10 SDK). No UI NuGet package: the modern look is custom XAML styles.

## 4. Architecture
```
FastDbExplorer.Wpf  ──►  FastDbExplorer.Application (interfaces)  ──►  FastDbExplorer.Domain (models)
        │                         ▲
        └──► FastDbExplorer.Infrastructure (SqlClient, JSON store) ──┘
tests/FastDbExplorer.Tests ──► Infrastructure
```
- **Domain:** plain models (`ConnectionSettings`, `SavedConnection`, `TableInfo`, `ColumnInfo`, `PageRequest`, `PageResult`, `FilterCondition`, `DatabaseAccessException`). No framework references.
- **Application:** interfaces only (`IDatabaseMetadataService`, `IConnectionProfileStore`, `IMapSource`, `IMapSourceFactory`).
- **Infrastructure:** `SqlServerMetadataService`, `SelectQueryBuilder`, `ValueConverter`, `ReadOnlySqlGuard`, `SqlConnectionStringFactory`, `JsonConnectionProfileStore`, and `Maps/` (`MapSourceFactory`, `MbTilesMapSource`, `MvtLayerReader`).
- **Wpf (composition root):** DI in `App.xaml.cs`; after splash and demo login, `MainViewModel` hosts Home, Connections, Explorer, Map, and Monitoring; Connections manages named profiles and the shell selector reconnects into a fresh session.
- **Errors:** `SqlException` → `DatabaseAccessException(kind)` in Infrastructure → Persian text via `Strings.Describe` in the UI.

### File map
```
    CLAUDE.md
    Directory.Build.props
    FastDbExplorer.slnx
    docs
    publish.cmd
    setup-map-assets.cmd
    setup-map-assets.ps1
    src
    tests
    docs/CODE_MAP.md
    docs/DECISIONS.md
    docs/KNOWN_ISSUES.md
    docs/PROGRESS.md
    docs/PROJECT_MASTER_CONTEXT.md
    docs/RUNBOOK.md
    src/FastDbExplorer.Application/Abstractions/Abstractions.cs
    src/FastDbExplorer.Application/Abstractions/MapAbstractions.cs
    src/FastDbExplorer.Application/FastDbExplorer.Application.csproj
    src/FastDbExplorer.Domain/FastDbExplorer.Domain.csproj
    src/FastDbExplorer.Domain/MapModels.cs
    src/FastDbExplorer.Domain/Models.cs
    src/FastDbExplorer.Domain/QueryModels.cs
    src/FastDbExplorer.Infrastructure/FastDbExplorer.Infrastructure.csproj
    src/FastDbExplorer.Infrastructure/JsonConnectionProfileStore.cs
    src/FastDbExplorer.Infrastructure/Maps/MapSourceFactory.cs
    src/FastDbExplorer.Infrastructure/Maps/GmdbMapSource.cs
    src/FastDbExplorer.Infrastructure/Maps/MbTilesMapSource.cs
    src/FastDbExplorer.Infrastructure/Maps/MvtLayerReader.cs
    src/FastDbExplorer.Infrastructure/ReadOnlySqlGuard.cs
    src/FastDbExplorer.Infrastructure/SelectQueryBuilder.cs
    src/FastDbExplorer.Infrastructure/SqlConnectionStringFactory.cs
    src/FastDbExplorer.Infrastructure/SqlServerMetadataService.cs
    src/FastDbExplorer.Infrastructure/ValueConverter.cs
    src/FastDbExplorer.Wpf/App.xaml
    src/FastDbExplorer.Wpf/App.xaml.cs
    src/FastDbExplorer.Wpf/Assets/Fonts/README.txt
    src/FastDbExplorer.Wpf/Assets/Map/README.txt
    src/FastDbExplorer.Wpf/Assets/Map/map.html
    src/FastDbExplorer.Wpf/Assets/app.ico
    src/FastDbExplorer.Wpf/FastDbExplorer.Wpf.csproj
    src/FastDbExplorer.Wpf/Localization/Strings.cs
    src/FastDbExplorer.Wpf/MainWindow.xaml
    src/FastDbExplorer.Wpf/MainWindow.xaml.cs
    src/FastDbExplorer.Wpf/Services/FileDialogService.cs
    src/FastDbExplorer.Wpf/Services/ThemeService.cs
    src/FastDbExplorer.Wpf/Themes/Dark.xaml
    src/FastDbExplorer.Wpf/Themes/Light.xaml
    src/FastDbExplorer.Wpf/Themes/Styles.xaml
    src/FastDbExplorer.Wpf/ViewModels/ConnectionViewModel.cs
    src/FastDbExplorer.Wpf/ViewModels/ExplorerViewModel.cs
    src/FastDbExplorer.Wpf/ViewModels/FilterRowViewModel.cs
    src/FastDbExplorer.Wpf/ViewModels/MainViewModel.cs
    src/FastDbExplorer.Wpf/ViewModels/MapViewModel.cs
    src/FastDbExplorer.Wpf/ViewModels/TableQueryViewModel.cs
    src/FastDbExplorer.Wpf/Views/ConnectionView.xaml
    src/FastDbExplorer.Wpf/Views/ConnectionView.xaml.cs
    src/FastDbExplorer.Wpf/Views/ExplorerView.xaml
    src/FastDbExplorer.Wpf/Views/ExplorerView.xaml.cs
    src/FastDbExplorer.Wpf/Views/MapView.xaml
    src/FastDbExplorer.Wpf/Views/MapView.xaml.cs
    src/FastDbExplorer.Wpf/Views/TableQueryView.xaml
    src/FastDbExplorer.Wpf/Views/TableQueryView.xaml.cs
    tests/FastDbExplorer.Tests/FastDbExplorer.Tests.csproj
    tests/FastDbExplorer.Tests/InfrastructureTests.cs
    tests/FastDbExplorer.Tests/MapSourceTests.cs
    tests/FastDbExplorer.Tests/QueryBuilderTests.cs
```

## 5. Key flows (where to look)
- **Connect:** `ConnectionsView.xaml` → `ConnectionViewModel` profile save/select → SQL password prompt when needed → `GetDatabasesAsync` → event `Connected` → `MainViewModel.OnConnected` creates a new Explorer and Monitoring session after shutting down the previous one.
- **Tables:** `ExplorerViewModel.LoadTablesAsync` → `GetTablesAsync` (`sys.tables` + `sys.partitions`, no scan). Selecting a table creates a `TableQueryViewModel`.
- **Query/Run:** `TableQueryViewModel.RunAsync` → `PageRequest` → `GetPageAsync` → `GetColumnsAsync` (validates names, finds PK) → read-only paging capability probe (server version + current DB compatibility, cached per server/database) → `SelectQueryBuilder.Build` (`OFFSET/FETCH` or `ROW_NUMBER()` fallback) → guard → reader (pageSize+1 rows). No database settings or schema are changed.
- **Paging:** stack of `PageCursor(offset, afterKey)`. OFFSET below 10,000 rows; keyset (`key > lastKey`, composite keys expanded to an OR chain) from there on; Previous pops the stack. Keyset only when the PK is usable; otherwise user-chosen order column + OFFSET only.
- **Theme:** `ThemeService` reads the Windows setting, swaps `Themes/Light.xaml`/`Dark.xaml` (merged dictionary **index 0**), sets dark title bar. All styles use `DynamicResource`.
- **Texts:** everything user-facing is in `Localization/Strings.cs` (`x:Static` in XAML). Ready for .resx later.
- **Result grid:** columns are created at run time in `TableQueryView.xaml.cs`; `GridRow` cells retain raw values, show concise previews, expose full text in tooltips/clipboard, and selected rows can be mapped. Query export streams every matching page into an `.xlsx` without changing the database.

- **Map:** `MapViewModel.OpenPathAsync` → `MapSourceFactory` (by extension) → `MbTilesMapSource` (SQLite, read-only). `MapView` hosts WebView2: page `Assets/Map/map.html` is served from virtual host `https://app.local`; tile URLs `https://tiles.local/{z}/{x}/{y}` are intercepted in `OnTileRequested` and answered from the open file (gzip vector tiles are sent with `Content-Encoding: gzip`; XYZ y is flipped to TMS rows). Host↔page messages (JSON): to page `load|layer|fit`, from page `ready|cursor|zoom`. Style is generated generically in JS (fill/line/circle per vector layer, colour hashed from layer name) so any vector file renders; layers panel toggles `<layer>|fill|line|circle`.

## 6. UI/UX system
- Persian, `FlowDirection=RightToLeft` on the window. **WPF mirrors layout** (alignment, margins, borders): write XAML as if LTR; "Left" means visual start in RTL. Technical content (server, SQL names, tables, grid) is forced `LeftToRight`.
- Font stack: Vazirmatn (optional, drop `.ttf` in `Assets/Fonts`) → Segoe UI Variable → Segoe UI → Tahoma. Icons: Segoe Fluent Icons / MDL2 glyphs.
- Tokens (Light / Dark): Bg `#F4F6FA`/`#0E1220`, Surface `#FFFFFF`/`#171C2E`, SurfaceAlt `#EEF1F7`/`#1F2540`, Line `#E0E5EE`/`#2B3252`, Text `#151B2C`/`#E8EBF5`, Muted `#6B7488`/`#98A1BC`, Accent `#4F46E5`/`#8A90FF`, Danger/Success have soft variants. Brand gradient `#4F46E5→#7C3AED`.
- Custom styles in `Themes/Styles.xaml`: Button (Primary/Icon/Row/Link), TextBox, PasswordBox, CheckBox, RadioButton segment, ListBox(Item), ComboBox(Item), DataGrid(+header/row/cell), ScrollBar (vertical+horizontal), ProgressBar, Card.
- Every screen needs: loading, empty, error, success states; keyboard access (Enter = Connect, F5 = Run); no layout jumps.

## 7. Decisions log
| # | Decision | Why |
|---|---|---|
| D1 | SQL Server first; WPF; .NET 10; MVVM | Master Context |
| D2 | Auth: Windows + SQL | owner choice |
| D3 | UI Persian RTL, auto light/dark, own styles | owner choice |
| D4 | Recent connections saved **without** passwords | security |
| D5 | `TrustServerCertificate` default ON (internal self-signed certs); user can turn it off | usability on internal servers |
| D6 | System DBs (id 1–4) hidden | tool is for user data |
| D7 | Row counts from `sys.partitions` (approximate) | no table scans |
| D8 | Paging hybrid OFFSET→keyset; no `COUNT(*)` | owner choice; speed on huge tables |
| D9 | No PK ⇒ user picks the order column; OFFSET only | keyset on non-unique column skips rows |
| D10 | Default isolation (READ COMMITTED), **no NOLOCK** | NOLOCK can return wrong data; revisit if blocking appears |
| D11 | Filter only common types (numbers, dates, text, guid, bit) | typed parameters keep indexes usable |
| D12 | Floating NuGet versions for now | no network in AI sandbox |
| D13 | Map renderer = MapLibre GL JS in WebView2 (owner delegated the choice) | best vector-tile support; tiles served from the file by request interception (no server/port) |
| D14 | New packages approved implicitly by owner delegation: Microsoft.Data.Sqlite, Microsoft.Web.WebView2 | needed for MBTiles + rendering |
| D15 | MapLibre files are downloaded by `setup-map-assets.cmd` (not committed); `publish.cmd` calls it if missing | AI has no internet; keeps repo small |
| D16 | GMDB = GMap.NET tile cache: SQLite `Tiles(id,X,Y,Zoom,Type,CacheTime)` + `TilesData(id,Tile)`, raster PNG/JPEG, XYZ y (no flip); shown via `GmdbMapSource`, provider (`Type`) with most tiles is used; MBTiles-layout fallback kept | verified against the owner's sample `DataExp.gmdb` (414 tiles, z0–13, one provider) |
| D17 | OSM PBF (`*.osm.pbf`) is NOT rendered directly; UI explains conversion. Converter (tilemaker/Planetiler, bbox, progress, cancel) is a later slice | raw OSM is not tiles; asia-latest is huge |
| D18 | Labels (place, road, water, poi, house number...) for OpenMapTiles-style vector files; glyph PBFs (`NotoSans`) and the RTL plugin are downloaded by `setup-map-assets.cmd` into `Assets/Map`; name priority `name:fa` → `name_en` → `name:latin` → `name` | owner saw a map without any names; fonts are plain local files, so no internet at run time |
| D19 | Tile requests from Web Workers are intercepted with `AddWebResourceRequestedFilter(uri, context, RequestSourceKinds.All)`; gzip tiles are unzipped in C# | MapLibre fetches tiles inside workers; the 2-argument filter only sees the page (map stayed blank) |
| D27 | Paging detects SQL Server version/compatibility with `SELECT`, uses a read-only `ROW_NUMBER()` fallback when `OFFSET/FETCH` is unsupported, and reports exact capability details if neither works | Support older SQL Server compatibility levels without changing the database |

## 8. Lessons learned (bugs we already hit — do not repeat)
- **L1 Blank screen:** an `x:Class` view without a code-behind calling `InitializeComponent()` builds fine but renders empty. Check every new View.
- **L2 Namespace clash:** `FastDbExplorer.Application` shadows `Application`. In WPF code write `System.Windows.Application` or a using alias.
- **L3 localhost vs 127.0.0.1:** `localhost` can connect via Shared Memory; `127.0.0.1` needs TCP/IP enabled in SQL Server Configuration Manager (+ service restart). Named instance: `127.0.0.1\SQLEXPRESS`; port: `127.0.0.1,1433`.
- **L4 Guard false positives:** a column named `[Update]`/`[Delete]` must not trip the read-only guard (identifiers are stripped before checking).
- **L5 Sandbox shell:** the AI sandbox uses `sh` (no `{a,b}` brace expansion). Create directories explicitly.
- **L6 ComboBox/DataGrid** need custom templates for dark mode; the theme's built-in look is light-only.

- **L7 WebView2 "airspace":** it is a native window; nothing from WPF can be drawn on top of it. Use panels beside it, or swap it out (Visibility) for state screens.
- **L8 Style hacks:** never put an implicit `Style` in `Border.Resources` to style the outer border — it restyles nested borders too. Use `Border.Style` with `BasedOn` + `DataTrigger`. Inverse bool→Visibility = `DataTrigger`, not a null converter.
- **L9 Static checks the AI can run:** XML well-formed, code-behind present, `StaticResource` keys exist, every `Strings.X` used in XAML exists.

## 9. Map module (slice 1 delivered, UNVERIFIED — never compiled/run)
**Owner request:** open maps in MBTiles (vector), GMDB and PBF; whichever is loaded must display and be workable; the owner will list map actions later; keep/upgrade UI/UX; keep docs current.
**Entry points:** "باز کردن نقشه" link on the connection screen, and a globe icon in the explorer sidebar; back button returns to where the user came from (`MainViewModel._returnTo`).

| Format | Status |
|---|---|
| `.mbtiles` vector (pbf) | ✅ implemented: layers from metadata `json`/`vector_layers`, or sampled from tiles (`MvtLayerReader`) |
| `.mbtiles` raster (png/jpg/webp) | ✅ implemented |
| `.gmdb` | ✅ GMap.NET cache (raster) via `GmdbMapSource`; MBTiles-layout fallback; otherwise the UI lists its tables. Not compiled/run by the AI. |
| `.pbf` = OSM raw (`asia-latest.osm.pbf`, Geofabrik) | ❌ not renderable directly (not tiles, huge). UI explains. Plan: converter slice (external tilemaker/Planetiler, extract only the needed bbox/region, progress + cancel) producing MBTiles |
| `.pbf` = MVT tile folder | not implemented (owner's PBF is OSM) |

**Facts:** MBTiles = SQLite `metadata(name,value)` + `tiles(zoom_level,tile_column,tile_row,tile_data)`; `tile_row` is TMS (`xyz_y = 2^z−1−row`); vector tiles are gzip MVT protobuf. OSM PBF = zlib blobs of nodes/ways/relations (header block type `OSMHeader`).
**Limits:** WebView2 runtime required (Edge WebView2 Runtime; present on Windows 11, usually on 10). WebView2 is a native window: WPF elements cannot overlay the map (hence side panels/toolbars only). Drag-and-drop of a file works on the empty state only. Style is generic (no labels, auto colours).
**Next slices (suggested order):** (1) owner builds & tests current WPF changes and the map with real data; (2) ~~GMDB real support~~ done (unverified); (3) PBF converter; (4) owner-specified map tools (feature inspect, measurement, search, import of coordinates, export…); (5) labels/glyphs offline bundle and custom styles.

## 10. Roadmap
| Phase | Scope | Status |
|---|---|---|
| MVP-01 | connect, databases, tables, search | ✅ done, owner-verified |
| MVP-02 | columns, filters, parameterized SQL, hybrid paging, cancel | ✅ delivered, owner reports it works (details not verified by AI) |
| Map module S1 | MBTiles viewer (vector+raster), provisional GMDB, PBF guidance | 🟡 delivered, unverified |
| Map module S2 | real GMDB, labels, tile-interception fix | 🟡 delivered, unverified |
| Map module S3+ | PBF converter, owner-defined tools | planned |
| MVP-03 | virtualized/streaming viewer, header sorting, column management | planned |
| MVP-04 | more providers, query history, audit, advanced security | planned |
Out of scope so far: editing data and other DB providers. Query-result Excel export is implemented in code but unverified.

## 11. How to build, test, publish (Windows, .NET 10 SDK)
- **First time for maps:** run `setup-map-assets.cmd` (downloads MapLibre; needs internet). Then build.
- Build: `dotnet build FastDbExplorer.slnx` · Run: `dotnet run --project src/FastDbExplorer.Wpf` · Tests: `dotnet test`.
- Offline release: double-click `publish.cmd` (tests → self-contained win-x64 publish → zip in `publish\`). Target PC needs no .NET.
- Saved connections: `%AppData%\FastDbExplorer\connections.json`.

## 12. Working agreement for vibe coding (best practices)
**Before coding:** read this file → restate scope in 3 lines → ask only blocking questions (max 5) → get approval for anything larger than a small slice or any new dependency.
**While coding:** one vertical slice at a time (Domain → Application → Infrastructure → ViewModel → View → Tests). No premature abstractions. Texts only in `Strings.cs`. Async + `CancellationToken` everywhere I/O happens.
**Checklists**
- *New View:* has code-behind with `InitializeComponent()`; all brushes `DynamicResource`; works in light+dark; RTL checked; loading/empty/error states; keyboard path.
- *New query feature:* typed parameters, identifiers validated, guard passes, paging, cancel, timeout, test added (`SelectQueryBuilderTests` style).
- *New dependency:* ask owner; pin version; record in D-log.
**Before saying "done":** build result, tests result, what was run by a human, remaining risks. Never claim completion without verification; label unverified work clearly.
**Report format after each slice:** Changed files · Reason · Tests performed · Build result · Remaining risks.
**AI environment reality:** AI sandbox = Linux, no .NET, no internet. AI can write code, run static checks (XML well-formedness, resource keys, code-behind presence) and produce zips. The human builds on Windows and pastes errors back; the AI fixes them.
**Prompt starter for a new AI session:** "Read docs/PROJECT_MASTER_CONTEXT.md (this file) and CLAUDE.md. Current phase: <phase>. Task: <task>. Follow §2 hard rules and §12 working agreement. Ask blocking questions first."

## 13. Known issues / risks
- **Map slice 1 was never compiled or run.** Most likely trouble: WebView2 event/response API usage, `Microsoft.Web.WebView2.Wpf` XAML namespace, MapLibre style expressions, package versions (`10.*`, `1.*`).
- Map: labels need the font files from `setup-map-assets.cmd` (run it again once); a font stack lacking a script shows blanks for that script; large raster/vector files are fine (tiles are read on demand) but each tile request opens a pooled SQLite connection.
- PBF (OSM): needs converter; see §9.
- Everything after MVP-01 was written without compiling in the AI sandbox; the owner reports it works, but treat edge cases as untested.
- `Contains`/`EndsWith` (LIKE '%x%') can't use indexes → may hit the 30 s timeout on huge tables.
- Deep OFFSET paging (tables without a usable PK) slows with depth.
- Huge text/binary cells are read fully before truncation (display cut at 300 chars).
- DateTime filters use a Jalali picker, store Gregorian SQL parameters with seconds fixed at `00`, and display DateTime result cells in Jalali. Numeric text values still use invariant culture.
- Animations ignore Windows "reduce motion". Icon glyph codes should be checked on Windows 10.
- Floating NuGet versions.
