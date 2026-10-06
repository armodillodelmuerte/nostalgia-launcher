# Manifest `launcher.json` (schema 1)

Every installed launcher reads the live manifest on start:

```
https://raw.githubusercontent.com/armodillodelmuerte/nostalgia-launcher/main/manifest/launcher.json
```

The URL is baked into the build (`BuiltInDefaults.ManifestUrl`). Edit `manifest/launcher.json`, commit, push to `main`; launchers see it on their next start (GitHub's raw cache: up to ~5 minutes). **No launcher release is needed** for a server move, new links, news, the beta notice or ending the beta.

## Loading rules

| Situation | Result |
|---|---|
| Remote manifest loads and is valid | used, and cached as `%LOCALAPPDATA%\Nostalgia\manifest-cache.json` |
| Remote fails (offline, timeout 8 s, HTTP error) or is invalid | last cached copy; the reason is in the log and the support info |
| Never loaded (offline first start, no cache) | built-in fallback: no server (Play disabled), launcher **0.x** builds show the built-in beta text, 1.x builds no phase |

Comments (`//`, `/* */`) and trailing commas are allowed. Unknown fields are ignored, so new fields never break old launchers. A higher `schemaVersion` is still read (known fields only). Testing: `Nostalgia.exe --manifest <file or URL>` (or env `NOSTALGIA_MANIFEST`) uses another manifest – see `tests/manifests/`.

A manifest is **rejected** (→ cache) when: `schemaVersion` < 1, `server.host` empty, a port is outside 1–65535, `server.quickbarUrl` is set but not `https://` (or `http://` to localhost), `launcher.minimumVersion`/`latestVersion` is not a version, `launcher.sha256` is set but not 64 hex characters, `client.supportedVersions`/`dllNames` is empty, or `phase` is present with an empty `id` or `noticeVersion` < 1. `Repo_manifests_are_valid` (unit test) checks every manifest in the repo.

## Fields

| Field | Type | Default | Meaning |
|---|---|---|---|
| `schemaVersion` | int | – (required) | 1 |
| `server.name` | string | Nostalgia | display name |
| `server.host` | string | – (required) | DNS name preferred (an IP move then needs only a DNS change) |
| `server.loginPort` | int | 10300 | login port: client connection and the status check (TCP connect, nothing sent) |
| `server.regionPort` | int | 10400 | informational |
| `server.quickbarUrl` | url | null | base URL of the quickbar endpoint: the launcher POSTs to `<url>/quickbar/pending` and `<url>/quickbar/ack`. HTTPS only (plain HTTP only to localhost, e.g. staging `http://127.0.0.1:10380`). Production: `https://connect.nostalgiapvp.com` (TLS front of the game server). Replaces `quickbarPort` of 0.1.x (old launchers ignore the new field). |
| `client.supportedVersions` | int[] | [1127] | accepted game.dll versions (1.127 = 1127, read from the PE version) |
| `client.dllNames` | string[] | ["game1127.dll", "game.dll"] | dll names searched in the client folder, in order |
| `client.infoUrl` | url | OpenDAoC client page | "Where do I get the client?" |
| `launcher.minimumVersion` | version | 0.0.0 | below it: update required, Play disabled |
| `launcher.latestVersion` | version | 0.0.0 | newer than the running build → update offered |
| `launcher.downloadUrl` | url | null | the release asset `Nostalgia.exe` (HTTPS only; HTTP only to localhost for tests) |
| `launcher.sha256` | hex | null | SHA-256 of that file; without it no update is offered |
| `launcher.releaseNotesUrl` | url | null | informational |
| `links.website` | url | null | "Website" button (hidden when null) |
| `links.discordInvite` | url | null | "Open Discord" (register/reset help) |
| `links.feedbackChannel` | url | null | beta strip link and dialog button (`https://discord.com/channels/<guild>/<channel>`) |
| `links.privacy` | url | null | reserved (the launcher has its own privacy text) |
| `bot.registerCommand` / `bot.resetCommand` | string | /register, /reset | shown in the help texts |
| `features.quickbars` | bool | false | quickbar step on Play (needs `server.quickbarUrl` and a launcher ≥ 0.2.0) – see below |
| `phase` | object or null | null | see below |
| `news[]` | list | [] | `title`, `date` (yyyy-MM-dd), `text`, `link` (optional); newest 8 shown |

Versions: `major.minor.patch` with optional `-pre` (`0.2.0-rc1` < `0.2.0`), leading `v` allowed. The launcher **never downgrades**: it only offers `latestVersion` if it is newer than itself.

## `phase` – beta UI

```json
"phase": {
  "id": "beta",
  "badge": "BETA",
  "title": "Nostalgia PvP Freeshard – Beta Test",
  "noticeDe": "Nostalgia ist ein PvP-Freeshard im Beta-Test. …",
  "noticeEn": "Nostalgia is a PvP freeshard in beta test. …",
  "noticeVersion": 1
}
```

| Phase present | Phase `null` / missing |
|---|---|
| gold badge (`badge`) next to the logo, drawn in the UI | no badge |
| window title `Nostalgia – PvP Freeshard (Beta Test)` (id `beta`; other ids: `Nostalgia – <title>`) | `Nostalgia` |
| footer strip "Beta test · Bugs and resets possible · Feedback on Discord" (link = `links.feedbackChannel`) | no strip |
| notice dialog (title + `noticeEn` – the UI is English; `noticeDe` is kept for the German texts elsewhere – button "Got it") on first start and whenever `noticeVersion` rises; stored per `id` in `settings.json` | no dialog |
| support info and About show `id (v<noticeVersion>)` | "keine" |

The texts are the same as in nostalgia-ops `config/beta.json` (Discord bot, in-game greeting) – keep them identical when changing one.

## Quickbars on Play

With `features.quickbars` true and `server.quickbarUrl` set, every Play runs this step before the client starts:

1. `POST <quickbarUrl>/quickbar/pending` `{account, password, launcherVersion}` (timeout 5 s). Any failure (offline, timeout, HTTP error, 401) → the game starts normally, one notice line under Play.
2. Nothing pending → the game starts.
3. Pending and a client process (`game1127.dll` / `game.dll` from `client.dllNames`, any folder) is running → nothing is written and the game is **not** started: "Close the game first, then press Play again."
4. Per character: INI = `%APPDATA%\Electronic Arts\Dark Age of Camelot\<settings>\<Name>-5.ini` (`<settings>` from the client's `paths.dat`; client quirk: the last character of an unterminated line is dropped, so `settings=Atlas1` gives `Atlas`). Missing → kept pending, "Log in with <Name> once, log out, then press Play again." Otherwise every `Hotkey_N` line of `[Quickbar]`, `[Quickbar2]`, `[Quickbar3]` is removed and the layout written (icon 0, the client fills it in); `[Panels]` `Quickbar2=`/`Quickbar3=` get fields 3 and 7 set to 1 when that bar has slots. All other lines, line endings and bytes stay.
5. `POST <quickbarUrl>/quickbar/ack` `{account, password, pendingIds}` with the written ids only; then the game starts: "Quickbars set up for <names>."

Server side: OpenDAoC custom code `QuickbarLauncherApi` (operator's repo); client file facts: operator's docs `client/quickbar-ini.md`. Off for everyone: `features.quickbars` false.

### How-tos

| Task | Change |
|---|---|
| Server moved | `server.host` (or only the DNS record) |
| New Discord invite / feedback channel | `links.discordInvite` / `links.feedbackChannel` |
| Show the beta notice again (e.g. after a wipe) | `phase.noticeVersion` + 1 |
| **End the beta** | `"phase": null` |
| Force an update | release first, then `launcher.latestVersion`, `downloadUrl`, `sha256`, and `minimumVersion` = the new version |
| News | add to `news` (newest first is not required, sorted by date) |
| Quickbar endpoint moved | `server.quickbarUrl` |
| Switch the quickbar step on/off | `features.quickbars` (on only once the production endpoint is live) |
