# How to apply this patch (manual push)

This zip contains ONLY the files that changed (repo-relative paths). It is cumulative: phases P1, P2, P3 and P4.

## 1) Clone and create a branch (cmd / PowerShell)
    git clone https://github.com/AliDevCodes/dbexplorer.git
    cd dbexplorer
    git checkout feature/monitoring-alerts
    git pull
    git checkout -b feature/nav-shell
(Already cloned? Skip the clone line.)

## 2) Extract the zip INTO the repo folder (choose "Replace")
Replaced files: MainViewModel.cs, MainWindow.xaml, MainWindow.xaml.cs, Views/ExplorerView.xaml. Everything else is new.

## 3) Run the helper script once (from the repo root)
    powershell -ExecutionPolicy Bypass -File .\apply-nav-shell.ps1
It removes the two old Back buttons (MapView.xaml, MonitoringView.xaml) and updates Strings.Tagline. No manual editing needed.
Then delete apply-nav-shell.ps1 and README-APPLY.md before committing if you do not want them in the repo.

## 4) Build, run, test
    dotnet build FastDbExplorer.slnx
    dotnet test
    dotnet run --project src/FastDbExplorer.Wpf
Check: rail on the right, Ctrl+1..4, Ctrl+K, connect lands on Home, DB picker in the top bar, light + dark theme, map opens.
If the build prints errors, paste them back and they will be fixed.

## 5) Commit and push
    git add -A
    git commit -m "feat(ui): navigation shell - rail, top bar, home, Ctrl+K palette (P1-P4)"
    git push -u origin feature/nav-shell
