# Releasing the launcher

## Normal release (GitHub Actions)

1. Art changed? `powershell -ExecutionPolicy Bypass -File tools/sync-art.ps1` (needs the art repo locally), commit `Assets/Brand/`.
2. Strings changed? `python tools/gen-strings.py`, commit.
3. `dotnet test` green.
4. Tag and push: `git tag v0.1.1` → `git push origin v0.1.1`. The workflow `.github/workflows/release.yml` runs the tests, publishes `Nostalgia.exe` (single file, self-contained, win-x64, version from the tag), and creates a **draft** release with `Nostalgia.exe`, `Nostalgia-<v>-win-x64.zip` (exe + LIESMICH.md + LICENSE + THIRD-PARTY-NOTICES.md + `licenses/`: font licences, Skia/HarfBuzz native notices), `SHA256SUMS.txt` and `manifest-snippet.txt`.
5. Check the draft (download the exe, `Get-FileHash`, start it), then **publish** it on GitHub.
6. Only then update `manifest/launcher.json` with the snippet (`latestVersion`, `downloadUrl`, `sha256`; `minimumVersion` too if old launchers must update), commit, push to `main`. Launchers offer the update on their next start.

Order matters: the manifest must never point to a draft (players can't download it) – the update would fail.

## Local build (no Actions)

```powershell
dotnet test
dotnet publish src/Nostalgia.Launcher -c Release -r win-x64 -p:Version=0.1.1 -o publish/win-x64
# release zip – same content as the workflow
New-Item -ItemType Directory -Force pkg/licenses, dist | Out-Null
Copy-Item publish/win-x64/Nostalgia.exe, LIESMICH.md, LICENSE, THIRD-PARTY-NOTICES.md pkg/
Copy-Item src/Nostalgia.Launcher/Assets/Brand/Fonts/OFL-*.txt, licenses/*.txt pkg/licenses/
Compress-Archive -Path pkg/* -DestinationPath dist/Nostalgia-0.1.1-win-x64.zip -Force
Copy-Item publish/win-x64/Nostalgia.exe dist/
Get-FileHash dist/* -Algorithm SHA256
```

The exe is ~56 MB (self-contained .NET 10 + Avalonia, compressed single file).

## Test an update locally

`tests/manifests/` holds staging manifests for the local server. For an update test, build two versions, serve the newer one with `python -m http.server 8765 --bind 127.0.0.1`, point a copy of a staging manifest at `http://127.0.0.1:8765/Nostalgia.exe` with its SHA-256 (HTTP is accepted only for localhost), and start the older exe with `--manifest <file>`. Use `NOSTALGIA_DATA_DIR=<temp dir>` so your real settings stay untouched.
