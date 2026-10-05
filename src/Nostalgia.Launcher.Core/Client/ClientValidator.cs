using Nostalgia.Launcher.Core.Manifest;

namespace Nostalgia.Launcher.Core.Client;

public enum ClientStatus
{
    Ok,
    FolderMissing,
    /// <summary>No game dll from <c>client.dllNames</c> in the folder.</summary>
    NoGameDll,
    /// <summary>game dll found, but not a supported version (e.g. patched to live).</summary>
    UnsupportedVersion,
    /// <summary>connect.exe missing (comes with the OpenDAoC client installer).</summary>
    NoConnectTool,
}

public sealed record ClientCheck(ClientStatus Status, string Folder, string? GameDll, int? Version, IReadOnlyList<string> FoundDlls)
{
    public bool Ok => Status == ClientStatus.Ok;
    public string? VersionText => Version is { } v ? PeVersionReader.ToClientString(v) : null;
}

/// <summary>Checks a client folder: a supported game dll (by PE version) and the connect tool. Read-only.</summary>
public static class ClientValidator
{
    public const string ConnectTool = "connect.exe";

    public static ClientCheck Check(string folder, ClientInfo client, string? preferredDll = null)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return new(ClientStatus.FolderMissing, folder, null, null, []);

        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferredDll)) names.Add(preferredDll);
        names.AddRange(client.DllNames.Where(n => !names.Contains(n, StringComparer.OrdinalIgnoreCase)));

        var found = new List<string>();
        (string Name, int Version)? supported = null;
        (string Name, int Version)? firstFound = null;
        foreach (var name in names)
        {
            string path = Path.Combine(folder, name);
            if (!File.Exists(path)) continue;
            found.Add(name);
            var v = PeVersionReader.ReadFileVersion(path);
            if (v is null) continue;
            int number = PeVersionReader.ToClientNumber(v);
            firstFound ??= (name, number);
            if (client.SupportedVersions.Contains(number)) { supported = (name, number); break; }
        }

        if (found.Count == 0)
            return new(ClientStatus.NoGameDll, folder, null, null, found);
        if (supported is null)
            return new(ClientStatus.UnsupportedVersion, folder, firstFound?.Name, firstFound?.Version, found);
        if (!File.Exists(Path.Combine(folder, ConnectTool)))
            return new(ClientStatus.NoConnectTool, folder, supported.Value.Name, supported.Value.Version, found);
        return new(ClientStatus.Ok, folder, supported.Value.Name, supported.Value.Version, found);
    }

    /// <summary>First valid folder among the saved one and the platform candidates; else the best failure (most specific).</summary>
    public static ClientCheck FindFirst(IEnumerable<string> folders, ClientInfo client, string? preferredDll = null)
    {
        ClientCheck? best = null;
        foreach (var f in folders.Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var c = Check(f, client, preferredDll);
            if (c.Ok) return c;
            if (best is null || Rank(c.Status) > Rank(best.Status)) best = c;
        }
        return best ?? new(ClientStatus.FolderMissing, "", null, null, []);
    }

    private static int Rank(ClientStatus s) => s switch
    {
        ClientStatus.NoConnectTool => 3,
        ClientStatus.UnsupportedVersion => 2,
        ClientStatus.NoGameDll => 1,
        _ => 0,
    };
}
