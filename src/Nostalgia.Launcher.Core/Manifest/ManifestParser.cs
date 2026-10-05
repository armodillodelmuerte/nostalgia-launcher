using Nostalgia.Launcher.Core.Updates;
using System.Text.Json;

namespace Nostalgia.Launcher.Core.Manifest;

public sealed record ManifestParseResult(LauncherManifest? Manifest, string? Error)
{
    public bool Ok => Manifest is not null;
}

/// <summary>Reads and checks <c>launcher.json</c>. Comments and trailing commas are allowed; unknown fields are ignored.</summary>
public static class ManifestParser
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public static ManifestParseResult Parse(string json)
    {
        LauncherManifest? m;
        try
        {
            m = JsonSerializer.Deserialize<LauncherManifest>(json, Options);
        }
        catch (JsonException e)
        {
            return new(null, $"JSON: {e.Message}");
        }

        if (m is null)
            return new(null, "empty manifest");

        var problems = Validate(m);
        return problems.Count == 0 ? new(m, null) : new(null, string.Join("; ", problems));
    }

    public static string Serialize(LauncherManifest manifest) => JsonSerializer.Serialize(manifest, Options);

    public static List<string> Validate(LauncherManifest m)
    {
        var p = new List<string>();
        if (m.SchemaVersion < 1)
            p.Add("schemaVersion missing or < 1");
        if (m.Server is null || string.IsNullOrWhiteSpace(m.Server.Host))
            p.Add("server.host missing");
        else
        {
            if (!IsPort(m.Server.LoginPort)) p.Add("server.loginPort out of range");
            if (!IsPort(m.Server.QuickbarPort)) p.Add("server.quickbarPort out of range");
        }

        if (m.Launcher is null)
            p.Add("launcher missing");
        else
        {
            if (!LauncherVersion.TryParse(m.Launcher.MinimumVersion, out _)) p.Add("launcher.minimumVersion invalid");
            if (!LauncherVersion.TryParse(m.Launcher.LatestVersion, out _)) p.Add("launcher.latestVersion invalid");
            if (m.Launcher.Sha256 is { Length: > 0 } sha && !UpdateVerifier.IsSha256Hex(sha)) p.Add("launcher.sha256 is not 64 hex chars");
        }

        if (m.Client is null || m.Client.SupportedVersions.Count == 0 || m.Client.DllNames.Count == 0)
            p.Add("client.supportedVersions/dllNames empty");

        if (m.Phase is { } ph)
        {
            if (string.IsNullOrWhiteSpace(ph.Id)) p.Add("phase.id missing");
            if (ph.NoticeVersion < 1) p.Add("phase.noticeVersion < 1");
        }

        return p;
    }

    private static bool IsPort(int port) => port is > 0 and <= 65535;
}
