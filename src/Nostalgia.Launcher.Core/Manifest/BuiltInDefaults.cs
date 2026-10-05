namespace Nostalgia.Launcher.Core.Manifest;

/// <summary>Values baked into the build.</summary>
public static class BuiltInDefaults
{
    /// <summary>Where the live manifest lives (raw file in the public launcher repo).</summary>
    public const string ManifestUrl =
        "https://raw.githubusercontent.com/armodillodelmuerte/nostalgia-launcher/main/manifest/launcher.json";

    /// <summary>Beta text (same as nostalgia-ops <c>config/beta.json</c>, Handover session 2026-10-05). The UI shows the English text.</summary>
    public const string BetaNoticeDe =
        "Nostalgia ist ein PvP-Freeshard im Beta-Test. Rechne mit Fehlern, Neustarts und Balance-Änderungen. " +
        "Fortschritt kann vor dem offiziellen Start zurückgesetzt werden. Feedback und Bugs bitte im Discord.";

    public const string BetaNoticeEn =
        "Nostalgia is a PvP freeshard in beta test. Expect bugs, restarts and balance changes. " +
        "Progress may be reset before the official launch. Please report feedback and bugs on Discord.";

    public static readonly PhaseInfo BetaPhase = new()
    {
        Id = "beta",
        Badge = "BETA",
        Title = "Nostalgia PvP Freeshard – Beta Test",
        NoticeDe = BetaNoticeDe,
        NoticeEn = BetaNoticeEn,
        NoticeVersion = 1,
    };

    /// <summary>
    /// Used only when no manifest was ever loaded (offline first start, no cache). It has no server, so playing
    /// needs a manifest. Builds with version 0.x carry the beta phase; 1.x+ builds show no phase without a manifest.
    /// </summary>
    public static LauncherManifest Fallback(LauncherVersion buildVersion) => new()
    {
        SchemaVersion = LauncherManifest.SupportedSchemaVersion,
        Server = new ServerInfo { Host = "" },
        Launcher = new LauncherRelease { MinimumVersion = "0.0.0", LatestVersion = buildVersion.ToString() },
        Phase = buildVersion.Major == 0 ? BetaPhase : null,
    };
}
