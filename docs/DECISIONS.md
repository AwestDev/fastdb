# Decisions
- 2026: WPF kept (per Master Context); modern look comes from custom styles, no UI NuGet package.
- UI is Persian RTL; technical fields (server, names, lists) are forced LTR.
- Theme follows Windows automatically via registry + `UserPreferenceChanged`.
- Recent connections saved WITHOUT passwords.
- `TrustServerCertificate` defaults to ON for internal servers (self-signed certs); user can turn it off.
- System databases (id 1-4) hidden from the list.
- Row counts come from `sys.partitions` (approximate, instant) instead of `COUNT(*)`.
- WPF code must write `System.Windows.Application` in full: the namespace `FastDbExplorer.Application` shadows the short name.
- MVP-02 paging is hybrid (owner's choice): OFFSET first, keyset for depth. No COUNT(*) ever; "has next page" comes from fetching pageSize+1 rows.
- No primary key -> the user must pick the order column (owner's choice); such tables use OFFSET only and ties are not guaranteed stable.
- Default isolation level is untouched (READ COMMITTED). NOLOCK was NOT used because it can return wrong data; revisit if production blocking appears.
- Filtering is limited to common types (numbers, dates, text, guid, bit). Other types can be shown but not filtered.
- `ReadOnlySqlGuard` ignores [quoted identifiers] and 'literals', so columns named [Update]/[Delete] work.
- Map decisions D13–D19 are in PROJECT_MASTER_CONTEXT §7 (MapLibre+WebView2, assets via setup-map-assets.cmd, GMDB = GMap.NET raster cache, OSM PBF needs converter, labels via local glyph files, worker tile interception).

## Excel coordinate layers (numbering continues after D19; not yet merged into Master Context §7)
- **D20 — ExcelDataReader 3.\*** for `.xlsx` and `.xls`: read-only, streaming, both formats in one package. Alternatives rejected: ClosedXML (no `.xls`), NPOI (much heavier, write support not needed). **Needs owner approval (new dependency); version is floating like the others — pin after first restore.**
- **D21 — Header contract:** first non-blank row of the first worksheet; exact names `Name`, `Latitude`, `Longitude`, matched case-insensitively with spaces ignored; any order; extra columns ignored; no auto-detection (a later phase).
- **D22 — Lenient rows, strict files:** invalid rows are skipped and reported, valid rows are imported; a file-level problem gives no layer. Errors are `ImportIssueCode` values (+ row/column + English detail); Persian text is added in the UI phase (same pattern as `DatabaseErrorKind`). Max 1,000 issues returned. Rows without a name are invalid (no invented names).
- **D23 — Coordinates:** WGS84 decimal degrees only. Text cells may use Persian/Arabic digits, the Arabic decimal separator and a single decimal comma (the owner works in Persian Excel). DMS/DDM text is rejected until its own phase. `NaN`/`Infinity` are rejected.
- **D24 — Radius is metres, always:** `MapLayer.RadiusMeters`, 0 ≤ r ≤ 20,037,508 (half of Earth's circumference), default 0 = none. Unit conversion for the user belongs to the UI edge.
- **D25 — Persistence:** one JSON file per layer (`coordinate-layers\{id}.json`), version field, temp-file + move writes, separate on-disk records. Unreadable/newer/rule-breaking files are skipped and kept, never deleted. A single file per layer keeps saves small and failures isolated.
- **D26 — No SQL involved:** this feature never touches SQL Server, so the read-only hard rules are unaffected; nothing sensitive is stored.
- **D27 — Adaptive read-only paging:** inspect SQL Server product version and the current database compatibility level with `SELECT`; use `OFFSET/FETCH` when supported, otherwise use the `ROW_NUMBER()` paging query when available. Never alter database compatibility or schema. If neither query form is supported, show the detected version, compatibility level, and required minimums.
