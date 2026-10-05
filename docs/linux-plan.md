# Linux plan (Wine)

Status 0.1.0: the launcher **builds and starts on Linux** (`dotnet publish src/Nostalgia.Launcher -c Release -r linux-x64`): manifest, news, server status, beta UI and self-update logic are platform-free. Finding the client, starting it and storing the login are stubs (`src/Nostalgia.Launcher.Platform/Linux`); the UI then says "Auf diesem Betriebssystem kann der Launcher den Client noch nicht starten". This page lists what a real implementation needs.

## What already is platform-free

| Part | Where |
|---|---|
| Manifest, phase/beta notice, versions, update planning + SHA-256 check | `Nostalgia.Launcher.Core` |
| game.dll version check (own PE reader – `FileVersionInfo` reads PE resources only on Windows) | `Core/Client/PeVersionReader.cs`, `ClientValidator.cs` |
| Status check (TCP), support info, settings (`~/.local/share/Nostalgia`) | `Core` |
| UI (Avalonia, X11/Wayland via XWayland) | `Nostalgia.Launcher` |

## To build

| Interface | Linux implementation | Notes |
|---|---|---|
| `IClientLocator` → `WineClientLocator` | Scan Wine prefixes for a folder with `game1127.dll` + `connect.exe`: `$WINEPREFIX`, `~/.wine`, Lutris (`~/Games/*`, prefixes from `~/.config/lutris/games/*.yml` → `game.prefix`), Bottles (`~/.local/share/bottles/bottles/*`), Steam/Proton (`~/.steam/steam/steamapps/compatdata/*/pfx`, non-Steam shortcut). Within a prefix: `drive_c/**/` up to depth 4, plus the OpenDAoC uninstall key from `system.reg` (`InstallLocation`, a `C:\…` path → `drive_c/…`). | The prefix must be remembered with the folder (settings: `wineprefix`). |
| `IGameLauncher` → `WineGameLauncher` | `WINEPREFIX=<prefix> wine connect.exe "<dll>" host:port account password`, cwd = client folder. `connect.exe` itself restarts under `/usr/bin/wine` when it detects Mono – starting it with `wine` directly avoids that path. Optional `wine` binary from settings (Lutris/Proton runners: `~/.local/share/lutris/runners/wine/<ver>/bin/wine`). | Needs .NET Framework 4 in the prefix (`winetricks dotnet40`/`dotnet48`) for connect.exe – the OpenDAoC installer brings it on Windows only. |
| `IProcessWatcher` → `WineProcessWatcher` | Find the process whose `/proc/<pid>/cmdline` contains the dll name (Wine shows the Windows image path; the process name is `wine-preloader`/`wine64-preloader` or the dll name). Exit via polling `/proc/<pid>`. | Early-exit hint works the same. |
| `ICredentialStore` → libsecret | Secret Service over D-Bus (`secret-tool`-compatible schema `org.nostalgia.Launcher`, attributes `account`), e.g. via `Tmds.DBus.Protocol` without a native dependency. Fallback when no keyring runs (headless/minimal WMs): ask the user, else session-only (as today). | Never a plain file. |
| `IQuickbarIniLocator` | `<prefix>/drive_c/users/<user>/AppData/Roaming/Electronic Arts/Dark Age of Camelot/<folder>/*.ini` | After the quickbar calibration. |
| Self-update | Exe name `Nostalgia` (no `.exe`), set the executable bit after the copy (`File.SetUnixFileMode`). A replaced running binary is fine on Linux (inode stays until exit). Release asset `Nostalgia-linux-x64` + its own `sha256` → manifest needs per-platform download fields (`launcher.downloads.linux-x64`) – schema 2, old fields stay for Windows. | |
| Packaging | Single-file binary (+ `.desktop` file and icon) or AppImage; Flatpak is awkward because Wine runs outside the sandbox. | |

## Test plan

1. Prefix with the OpenDAoC client (Lutris "Dark Age of Camelot" script or plain `wine OpenDAoCInstaller.exe`), `winetricks dotnet48`.
2. Manual: `WINEPREFIX=… wine connect.exe game1127.dll <host>:10300 <acc> <pw>` reaches character select (reference).
3. Locator unit tests with fake prefix trees (`~/.wine`, Lutris yml, Proton `pfx`).
4. Launcher on Ubuntu 24.04 (GNOME/Wayland), Fedora (KDE), Steam Deck desktop mode: first start → client found → beta dialog → login stored in the keyring → play → early-exit hint with a wrong password → self-update with a test manifest.
5. CI: build + unit tests on `ubuntu-latest` (already possible today).

## Effort estimate

| Part | Days |
|---|---|
| Locator (prefixes incl. Lutris/Steam/Bottles) + tests | 1.5 |
| Wine launcher + process watcher | 1 |
| libsecret store + fallback | 1 |
| Self-update (executable bit, per-platform manifest fields, schema 2) | 0.5 |
| Packaging (.desktop/AppImage) + CI job | 1 |
| Tests on 2–3 distros / Steam Deck, docs, LIESMICH section | 1.5 |
| **Total** | **≈ 6.5 days** (plus the quickbar INI path once the module exists) |
