using System.Runtime.Versioning;
using Nostalgia.Launcher.Core.Platform;
using Nostalgia.Launcher.Core.Quickbars;

namespace Nostalgia.Launcher.Platform.Windows;

/// <summary>
/// <c>%APPDATA%\Electronic Arts\Dark Age of Camelot\&lt;settings&gt;</c>, <c>&lt;settings&gt;</c> from the client's <c>paths.dat</c>
/// (owner's install: <c>Atlas</c>). Rules: docs/client/quickbar-ini.md, <see cref="QuickbarIniPaths"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsQuickbarIniLocator : IQuickbarIniLocator
{
    public static string DaocRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Electronic Arts", "Dark Age of Camelot");

    public string? SettingsFolder(string clientFolder) => QuickbarIniPaths.SettingsFolder(DaocRoot, clientFolder);
}
