# Nostalgia Launcher – Kurzanleitung

> **Nostalgia ist im Beta-Test.**
> Nostalgia ist ein PvP-Freeshard im Beta-Test. Rechne mit Fehlern, Neustarts und Balance-Änderungen. Fortschritt kann vor dem offiziellen Start zurückgesetzt werden. Feedback und Bugs bitte im Discord – im Feedback-Kanal (Link im Launcher unten links: „Feedback on Discord“).

Die Oberfläche des Launchers ist englisch; die Knöpfe sind unten mit ihrem englischen Namen genannt.

## 1. Client besorgen

Du brauchst einen Dark-Age-of-Camelot-Client mit **game1127.dll** und **connect.exe** im Ordner. Den bekommst du über den Installer auf der [OpenDAoC-Client-Seite](https://www.opendaoc.com/docs/client/). Der Launcher lädt keine Spieldateien herunter und verändert keine.

**Wichtig:** Starte nicht `camelot.exe` (den offiziellen Patcher). Der aktualisiert den Client auf die aktuelle Live-Version, und dann passt er nicht mehr.

## 2. Launcher laden und starten

1. `Nostalgia.exe` von der Release-Seite herunterladen und in einen eigenen Ordner legen, z. B. `Dokumente\Nostalgia`. Nicht in „Programme“ legen, sonst kann er sich nicht selbst aktualisieren.
2. Starten. Windows zeigt beim ersten Mal **„Der Computer wurde durch Windows geschützt“**, weil der Launcher nicht signiert ist. Klicke auf **„Weitere Informationen“** und dann auf **„Trotzdem ausführen“**.
3. Der Launcher sucht den Client selbst. Findet er ihn nicht, wähle den Client-Ordner mit **„Choose folder …“**.
4. Lies den Beta-Hinweis und klicke auf **„Got it“**.

## 3. Konto anlegen (Discord)

1. Tritt dem Nostalgia-Discord bei (Button „Open Discord“ im Launcher).
2. Schreib im Kanal: `/register <Name>`. Der Name darf nur Buchstaben und Ziffern enthalten, und jeder Discord-Nutzer bekommt ein Konto.
3. Der Bot zeigt dir **Kontoname und Passwort**. Kopiere beides in den Launcher. Leerzeichen und Zeilenumbrüche beim Einfügen sind kein Problem.
4. Lass „Remember login“ an. Das Passwort ist zufällig und wird verschlüsselt gespeichert, du musst es nie tippen.
5. Klicke auf **PLAY**. Der Client startet direkt in die Charakterauswahl.

## 4. Passwort verloren oder neuer PC

- Schreib im Discord `/reset`. Der Bot zeigt ein neues Passwort, das alte gilt dann nicht mehr.
- Im Launcher auf „/reset“ und dann auf **„Enter new password“** klicken (oder unter Settings auf „Replace password“). Neues Passwort einfügen und auf PLAY klicken; der Launcher ersetzt das gespeicherte.
- Auf einem neuen PC genauso vorgehen. Das gespeicherte Login gilt nur für deinen Windows-Benutzer auf diesem PC.

## 5. Wenn der Client gleich wieder zugeht

Der Client zeigt den Grund oben links im Ladebildschirm, über „Hit ESC to exit game.“. Danach zeigt der Launcher einen Hinweis:

| Meldung im Client | Bedeutung | Was tun |
|---|---|---|
| Your password is incorrect. | Passwort falsch | `/reset` im Discord, neues Passwort im Launcher eintragen |
| Your account has no access to this game. | Konto unbekannt oder nicht mit Discord verknüpft | Kontonamen prüfen, sonst einen GM im Discord ansprechen |
| nur „Hit ESC to exit game.“ | Server nicht erreichbar | Status oben rechts im Launcher prüfen, später nochmal |

Windows-Firewall: Wenn sie beim ersten Start fragt, erlaube den Zugriff für das Spiel. Wenn dein Virenscanner `connect.exe` meldet: Die Datei gehört zur Client-Installation und startet den Client für Freeshards.

## 6. Quickbars nach einem Respec

Der Quickbar-Import mit dem Code des Master Trainers kommt in einer späteren Version. Das Wiederherstellen eines Quickbar-Backups wird dann hier beschrieben.

## 7. Hilfe

Unter **Settings** findest du **„Copy support info“**. Füge den Text im Discord ein; er enthält Versionen und Pfade, aber nie dein Passwort. Unter „Open logs“ liegen die Protokolle der letzten Tage.

## Lizenz

Der Programmcode des Launchers steht unter der MIT-Lizenz (Datei `LICENSE`, Copyright (c) 2026 Nostalgia PVP Team). Ausgenommen sind die Grafiken (Logo, Hintergründe, Icon): Alle Rechte vorbehalten, sie dürfen nur unverändert im Nostalgia-Launcher genutzt werden. Die Schriften Cinzel und Inter stehen unter der SIL Open Font License (Ordner `licenses`). Fremdkomponenten wie Avalonia, SkiaSharp und Serilog sind in `THIRD-PARTY-NOTICES.md` aufgeführt.

Datenschutz: Der Server speichert deine Discord-ID und deinen Kontonamen. Der Launcher speichert nur auf deinem PC: das verschlüsselte Login, die Einstellungen und die Logs (Details im Launcher unter „Privacy“).

---
Nostalgia is a fan-run project and is not affiliated with or endorsed by Broadcom, Electronic Arts or Mythic Entertainment. Dark Age of Camelot is a trademark of its respective owner.
