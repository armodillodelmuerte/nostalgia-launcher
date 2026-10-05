using System.Runtime.Versioning;
using Nostalgia.Launcher.Core.Platform;

namespace Nostalgia.Launcher.Platform.Windows;

/// <summary>
/// Character INIs live in <c>%APPDATA%\Electronic Arts\Dark Age of Camelot\&lt;folder&gt;\&lt;Character&gt;-&lt;n&gt;.ini</c>
/// (owner's PC 2026-10-05: folder "Atlas"). Which folder belongs to which server is part of the quickbar calibration
/// (docs/client/quickbar-ini.md) – until then all are listed and the player picks.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsQuickbarIniLocator : IQuickbarIniLocator
{
    public IReadOnlyList<string> FindCharacterInis()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Electronic Arts", "Dark Age of Camelot");
        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateFiles(root, "*-*.ini", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
    }
}
