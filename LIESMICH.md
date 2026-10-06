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

## 6. Quickbars vom Master Trainer

Wenn du beim Master Trainer ein Spec-Preset wählst, legt der Server dafür passende Quickbars für diesen Charakter bereit. Der Launcher trägt sie beim nächsten Klick auf **PLAY** ein:

1. Preset beim Master Trainer wählen.
2. Ausloggen und den Client **ganz schließen**.
3. Im Launcher auf **PLAY** klicken. Der Launcher fragt den Server nach bereitliegenden Quickbars, trägt sie ein und startet dann das Spiel. Unter dem PLAY-Knopf steht dann **„Quickbars set up for <Name>.“**

**Achtung:** Die Quickbars **1, 2 und 3** des Charakters werden dabei **komplett ersetzt** – alle zehn Bänke, auch Gegenstände und Makros, die dort lagen. Alles andere (Fenster, Chat, Makro-Liste, Kamera …) bleibt, wie es war. Es gibt keine Sicherungskopie. Die Quickbars passen zum gewählten Preset; wenn du danach umskillst oder Fähigkeiten dazukommen, können einzelne Slots nicht mehr stimmen.

Meldungen unter dem PLAY-Knopf:

| Meldung | Bedeutung | Was tun |
|---|---|---|
| Quickbars set up for <Name>. | Eingetragen | nichts – einloggen und spielen |
| Close the game first, then press Play again. | Quickbars liegen bereit, aber ein Client läuft noch. Das Spiel wird nicht gestartet. | Client schließen, PLAY nochmal klicken |
| Log in with <Name> once, log out, then press Play again. | Für diesen Charakter gibt es auf diesem PC noch keine Einstellungsdatei (noch nie eingeloggt). Die Quickbars bleiben bereit. | Mit dem Charakter einmal einloggen, ausloggen, Client schließen, PLAY klicken |
| Quickbars could not be checked (…) – the game starts as usual. | Server nicht erreichbar oder Login abgelehnt. Das Spiel startet trotzdem. | Später nochmal PLAY; die Quickbars bleiben auf dem Server bereit |
| Quickbars for <Name> could not be written … | Die Datei war gesperrt oder nicht beschreibbar. | Client schließen, PLAY nochmal; sonst Support-Info im Discord posten |

Hinweis: Hat ein Charakter auf einem anderen OpenDAoC-Server denselben Namen, teilen sich beide dieselbe Einstellungsdatei im Client – dessen Quickbars werden dann mit ersetzt.

## 7. Hilfe

Unter **Settings** findest du **„Copy support info“**. Füge den Text im Discord ein; er enthält Versionen und Pfade, aber nie dein Passwort. Unter „Open logs“ liegen die Protokolle der letzten Tage.

## Lizenz

Der Programmcode des Launchers steht unter der MIT-Lizenz (Datei `LICENSE`, Copyright (c) 2026 Nostalgia PVP Team). Ausgenommen sind die Grafiken (Logo, Hintergründe, Icon): Alle Rechte vorbehalten, sie dürfen nur unverändert im Nostalgia-Launcher genutzt werden. Die Schriften Cinzel und Inter stehen unter der SIL Open Font License (Ordner `licenses`). Fremdkomponenten wie Avalonia, SkiaSharp und Serilog sind in `THIRD-PARTY-NOTICES.md` aufgeführt.

Datenschutz: Der Server speichert deine Discord-ID und deinen Kontonamen. Der Launcher speichert nur auf deinem PC: das verschlüsselte Login, die Einstellungen und die Logs (Details im Launcher unter „Privacy“). Beim Klick auf PLAY schickt er Kontoname und Passwort verschlüsselt (HTTPS) an den Nostalgia-Server, um bereitliegende Quickbars abzuholen.

---
Nostalgia is a fan-run project and is not affiliated with or endorsed by Broadcom, Electronic Arts or Mythic Entertainment. Dark Age of Camelot is a trademark of its respective owner.
