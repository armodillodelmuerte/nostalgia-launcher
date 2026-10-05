# SmartScreen, antivirus and code signing

`Nostalgia.exe` is **not signed**. Nothing has been bought.

## What players see

| Warning | When | What to do (also in LIESMICH.md) |
|---|---|---|
| Microsoft Defender SmartScreen: "Der Computer wurde durch Windows geschützt" | unsigned exe with no download reputation yet – every new version starts at zero | "Weitere Informationen" → "Trotzdem ausführen" |
| Browser: "wird selten heruntergeladen" | same reason (Edge/Chrome) | keep the download |
| Antivirus warning for **connect.exe** (not our file) | connect.exe writes into the game process memory (that is how freeshard clients start) | allow it; it belongs to the client installation |
| Antivirus heuristic on `Nostalgia.exe` | single-file .NET apps unpack themselves at start; rare | report as false positive to the vendor (Microsoft: https://www.microsoft.com/wdsi/filesubmission) |

Players can check the file against the SHA-256 in the release notes / `SHA256SUMS.txt` (`Get-FileHash Nostalgia.exe`).

## Signing options (researched 2026-10-05 – check prices before buying)

| Option | Cost | Fits Nostalgia? |
|---|---|---|
| **SignPath Foundation** (free for open source, signs in GitHub Actions) | free | Needs an OSI licence (the repo has none yet), public repo, free downloads, MFA for all maintainers, an existing release and "verifiable reputation". Certificate is issued to SignPath Foundation, not to us. Best value once the licence is set. |
| **Certum Open Source Code Signing** (OV, individual open-source developers) | ~49 €/year cloud (SimplySign) or ~69 € with card + reader in year 1, cheaper renewal | Identity check of a person; cloud variant works from CI. SmartScreen reputation still has to build up. |
| Commercial OV certificate (Sectigo, SSL.com, GlobalSign …) | ~200–400 €/year | Since 2023 keys must sit on hardware token/HSM (cloud HSM costs extra). Same reputation build-up. |
| EV certificate | ~300–600 €/year | No longer gives instant SmartScreen reputation; little gain over OV for us. |
| Azure Artifact Signing (formerly Trusted Signing) | ~10 $/month | Individuals only in USA/Canada; EU only as an organisation (company register) → not for a private person in Germany. |

Recommendation: decide the licence, then apply to SignPath Foundation; fall back to Certum Open Source if declined. Until then: document the SmartScreen steps (done in LIESMICH.md) and publish SHA-256 sums.

Sources: [Microsoft – code signing options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options), [Artifact Signing eligibility (Q&A)](https://learn.microsoft.com/en-us/answers/questions/2113965/trusted-signing-for-individual), [SignPath Foundation](https://signpath.org/), [Certum Open Source Code Signing](https://shop.certum.eu/open-source-code-signing-on-simplysign.html) – retrieved 2026-10-05.
