# Runbook
**Build/run (Windows, .NET 10 SDK):** `dotnet build FastDbExplorer.slnx` then `dotnet run --project src/FastDbExplorer.Wpf`.
**Tests (xUnit):** `dotnet test`. Test = a method marked `[Fact]` that checks one behaviour; `[Theory]` runs the same check with many inputs.
**Offline package:** double-click `publish.cmd` -> `publish\FastDbExplorer\FastDbExplorer.exe` (+ zip). Self-contained win-x64: no .NET needed on the target PC.
**Persian font:** optional — copy Vazirmatn `.ttf` files into `src/FastDbExplorer.Wpf/Assets/Fonts/` and rebuild.

**Maps:** run `setup-map-assets.cmd` once (downloads MapLibre into `src/FastDbExplorer.Wpf/Assets/Map`). `publish.cmd` does this automatically if the files are missing. Target PCs need the Edge WebView2 Runtime.
