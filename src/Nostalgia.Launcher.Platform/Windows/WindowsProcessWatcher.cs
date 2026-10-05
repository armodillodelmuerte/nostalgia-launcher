using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Nostalgia.Launcher.Core.Platform;

namespace Nostalgia.Launcher.Platform.Windows;

/// <summary>The game runs as a process named after the dll (game1127.dll → "game1127"). Matched by name and, where readable, by path.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsProcessWatcher : IProcessWatcher
{
    public IReadOnlyList<int> FindGameProcesses(string clientFolder, string gameDll)
    {
        string name = Path.GetFileNameWithoutExtension(gameDll);
        string expected = Path.GetFullPath(Path.Combine(clientFolder, gameDll));
        var ids = new List<int>();
        foreach (var p in Process.GetProcessesByName(name))
        {
            using (p)
            {
                string? path = null;
                try { path = p.MainModule?.FileName; }
                catch (Win32Exception) { }
                catch (InvalidOperationException) { continue; }
                if (path is null || string.Equals(Path.GetFullPath(path), expected, StringComparison.OrdinalIgnoreCase))
                    ids.Add(p.Id);
            }
        }
        return ids;
    }

    public async Task<int?> WaitForGameStartAsync(string clientFolder, string gameDll, IReadOnlySet<int> ignore, TimeSpan timeout, CancellationToken ct)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            var id = FindGameProcesses(clientFolder, gameDll).FirstOrDefault(i => !ignore.Contains(i));
            if (id != 0) return id;
            await Task.Delay(500, ct);
        }
        return null;
    }

    public async Task WaitForExitAsync(int pid, CancellationToken ct)
    {
        Process p;
        try { p = Process.GetProcessById(pid); }
        catch (ArgumentException) { return; }
        using (p)
        {
            try { await p.WaitForExitAsync(ct); }
            catch (InvalidOperationException) { }
        }
    }
}
