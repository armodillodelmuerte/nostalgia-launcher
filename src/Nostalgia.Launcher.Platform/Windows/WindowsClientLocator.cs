using System.Runtime.Versioning;
using Microsoft.Win32;
using Nostalgia.Launcher.Core.Platform;

namespace Nostalgia.Launcher.Platform.Windows;

/// <summary>
/// Candidate client folders, most likely first: OpenDAoC installer (uninstall key, InstallLocation), other uninstall
/// entries named Dark Age of Camelot / OpenDAoC, the EA key (InstallDir), then default paths. Read-only.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsClientLocator : IClientLocator
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string EaKeyPath = @"SOFTWARE\Electronic Arts\EA Games\Dark Age of Camelot";

    public IReadOnlyList<string> FindCandidateFolders()
    {
        var result = new List<string>();
        foreach (var (hive, view) in Hives())
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = root.OpenSubKey(UninstallPath);
                if (uninstall is not null)
                    foreach (var name in uninstall.GetSubKeyNames())
                    {
                        using var k = uninstall.OpenSubKey(name);
                        if (k?.GetValue("DisplayName") is not string display) continue;
                        bool openDaoc = display.Contains("OpenDAoC", StringComparison.OrdinalIgnoreCase);
                        if (!openDaoc && !display.Contains("Dark Age of Camelot", StringComparison.OrdinalIgnoreCase)) continue;
                        string? folder = k.GetValue("InstallLocation") as string;
                        if (string.IsNullOrWhiteSpace(folder) && k.GetValue("UninstallString") is string un)
                            folder = Path.GetDirectoryName(un.Trim('"'));
                        if (!string.IsNullOrWhiteSpace(folder))
                        {
                            if (openDaoc) result.Insert(0, Normalize(folder)); else result.Add(Normalize(folder));
                        }
                    }

                using var ea = root.OpenSubKey(EaKeyPath);
                if (ea?.GetValue("InstallDir") is string dir && !string.IsNullOrWhiteSpace(dir))
                    result.Add(Normalize(dir));
            }
            catch (System.Security.SecurityException) { }
            catch (UnauthorizedAccessException) { }
        }

        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        result.AddRange([
            @"C:\OpenDAoC",
            Path.Combine(pf86, "OpenDAoC"),
            Path.Combine(pf, "OpenDAoC"),
            Path.Combine(pf86, "Electronic Arts", "Dark Age of Camelot"),
            Path.Combine(pf86, "Mythic", "Camelot"),
            @"C:\Games\Electronic Arts\Dark Age of Camelot",
        ]);
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IEnumerable<(RegistryHive, RegistryView)> Hives() =>
    [
        (RegistryHive.LocalMachine, RegistryView.Registry64),
        (RegistryHive.LocalMachine, RegistryView.Registry32),
        (RegistryHive.CurrentUser, RegistryView.Registry64),
        (RegistryHive.CurrentUser, RegistryView.Registry32),
    ];

    private static string Normalize(string folder) => folder.Trim().Trim('"').TrimEnd('\\', '/');
}
