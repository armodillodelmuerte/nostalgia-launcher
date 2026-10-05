using System.Reflection;
using Avalonia.Platform;
using Nostalgia.Launcher.Core;

namespace Nostalgia.Launcher.Services;

public static class BuildInfo
{
    public static LauncherVersion Version { get; } = ReadVersion();

    /// <summary>Art repo commit from Assets/Brand/ART_VERSION.txt (written by tools/sync-art.ps1).</summary>
    public static string ArtVersion => _art.Value;
    private static readonly Lazy<string> _art = new(ReadArtVersion); // AssetLoader needs Avalonia initialised

    public const string RepoUrl = "https://github.com/armodillodelmuerte/nostalgia-launcher";

    private static LauncherVersion ReadVersion()
    {
        string? info = typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return LauncherVersion.TryParse(info, out var v) ? v : new LauncherVersion(0, 0, 0);
    }

    private static string ReadArtVersion()
    {
        try
        {
            using var s = AssetLoader.Open(new Uri("avares://Nostalgia/Assets/Brand/ART_VERSION.txt"));
            using var r = new StreamReader(s);
            string? line;
            while ((line = r.ReadLine()) is not null)
                if (line.StartsWith("art-commit:", StringComparison.Ordinal))
                    return line["art-commit:".Length..].Trim();
        }
        catch (Exception) { }
        return "unbekannt";
    }

    public static string ReadAsset(string relative)
    {
        using var s = AssetLoader.Open(new Uri("avares://Nostalgia/" + relative));
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
