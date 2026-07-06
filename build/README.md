# ScoreCode — Build Tools

## download-runtimes.ps1

Downloads portable runtimes for production bundling. Run **once** before building the Inno Setup installer.

```powershell
.\build\download-runtimes.ps1           # Download all runtimes
.\build\download-runtimes.ps1 -SkipExisting  # Skip already-downloaded
.\build\download-runtimes.ps1 -Force    # Re-download everything
```

### What it downloads

| Runtime | Version | Size | Target Folder |
|---------|---------|------|---------------|
| Python (embeddable) | 3.12.8 | ~15 MB | `runtimes/python-3.12/` |
| Node.js (portable) | 20.18.1 LTS | ~30 MB | `runtimes/node-20/` |
| OpenJDK (Microsoft) | 21.0.5 | ~180 MB | `runtimes/jdk-21/` |
| MinGW g++ (WinLibs) | 14.2.0 | ~200 MB | `runtimes/mingw64/` |

### Notes

- The `runtimes/` folder is **.gitignored** — only this script is version-controlled
- Downloads are cached in `%TEMP%\ScoreCode-runtime-downloads\`
- For **development**, use system PATH runtimes (install via `winget`)
- For **production**, run this script → build with Inno Setup
- `RuntimePaths.cs` detects bundled runtimes via `scorecode-runtimes.marker`
