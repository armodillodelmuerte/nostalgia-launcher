using System.Text.Json.Serialization;

namespace Nostalgia.Launcher.Core.Manifest;

/// <summary>
/// Remote manifest <c>launcher.json</c> (schema 1). Documented in <c>docs/manifest.md</c>.
/// Unknown fields are ignored, so newer manifests stay readable by older launchers.
/// </summary>
public sealed record LauncherManifest
{
    /// <summary>Highest schema this launcher understands. A higher value is still read (known fields only).</summary>
    public const int SupportedSchemaVersion = 1;

    public int SchemaVersion { get; init; }
    public ServerInfo Server { get; init; } = new();
    public ClientInfo Client { get; init; } = new();
    public LauncherRelease Launcher { get; init; } = new();
    public LinkInfo Links { get; init; } = new();
    public BotInfo Bot { get; init; } = new();
    public FeatureFlags Features { get; init; } = new();

    /// <summary>Phase block (beta …). <c>null</c> or missing = no phase UI at all.</summary>
    public PhaseInfo? Phase { get; init; }

    public IReadOnlyList<NewsItem> News { get; init; } = [];
}

public sealed record ServerInfo
{
    public string Name { get; init; } = "Nostalgia";
    /// <summary>DNS name preferred (a server move then needs no manifest change either).</summary>
    public string Host { get; init; } = "";
    public int LoginPort { get; init; } = 10300;
    public int RegionPort { get; init; } = 10400;
    public int QuickbarPort { get; init; } = 10380;
}

public sealed record ClientInfo
{
    /// <summary>Accepted game.dll versions as numbers (1.127 = 1127).</summary>
    public IReadOnlyList<int> SupportedVersions { get; init; } = [1127];
    /// <summary>game.dll file names to look for in the client folder, in order of preference.</summary>
    public IReadOnlyList<string> DllNames { get; init; } = ["game1127.dll", "game.dll"];
    /// <summary>Page that explains where to get a matching client (never a client download by us).</summary>
    public string? InfoUrl { get; init; }
}

public sealed record LauncherRelease
{
    public string MinimumVersion { get; init; } = "0.0.0";
    public string LatestVersion { get; init; } = "0.0.0";
    public string? DownloadUrl { get; init; }
    public string? Sha256 { get; init; }
    public string? ReleaseNotesUrl { get; init; }
}

public sealed record LinkInfo
{
    public string? Website { get; init; }
    public string? DiscordInvite { get; init; }
    /// <summary>Discord link to the bug/feedback channel (https://discord.com/channels/…).</summary>
    public string? FeedbackChannel { get; init; }
    /// <summary>Optional external privacy page; the launcher always has its own privacy text.</summary>
    public string? Privacy { get; init; }
}

public sealed record BotInfo
{
    public string RegisterCommand { get; init; } = "/register";
    public string ResetCommand { get; init; } = "/reset";
}

public sealed record FeatureFlags
{
    /// <summary>Quickbar import tab. Only shown when the flag is on AND the build contains the module.</summary>
    public bool Quickbars { get; init; }
}

public sealed record PhaseInfo
{
    public string Id { get; init; } = "";
    public string Badge { get; init; } = "";
    public string Title { get; init; } = "";
    public string NoticeDe { get; init; } = "";
    public string NoticeEn { get; init; } = "";
    /// <summary>Raise it to show the notice dialog to everyone again.</summary>
    public int NoticeVersion { get; init; } = 1;
}

public sealed record NewsItem
{
    public string Title { get; init; } = "";
    /// <summary>ISO date (yyyy-MM-dd).</summary>
    public string Date { get; init; } = "";
    public string Text { get; init; } = "";
    public string? Link { get; init; }

    [JsonIgnore]
    public DateOnly? ParsedDate => DateOnly.TryParseExact(Date, "yyyy-MM-dd", out var d) ? d : null;
}
