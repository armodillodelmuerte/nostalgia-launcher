using System.Diagnostics;
using System.Runtime.Versioning;
using Nostalgia.Launcher.Core.Platform;

namespace Nostalgia.Launcher.Platform.Windows;

/// <summary>
/// Starts the client exactly like the OpenDAoC launcher and the owner's local.bat:
/// <c>connect.exe "&lt;game dll&gt;" host:port account password</c>, working directory = client folder.
/// connect.exe (Dawn of Light, in the player's install) patches the game process in memory; we change no files.
/// The password is a command-line argument of connect.exe and game.dll – visible to local processes of the same user
/// (docs/client/launching.md). It is never logged.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsGameLauncher : IGameLauncher
{
    public Task<int> LaunchAsync(GameLaunchRequest r, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(r.ClientFolder, r.ConnectTool),
            WorkingDirectory = r.ClientFolder,
            UseShellExecute = false,
            // connect.exe is a console tool: no console window. Its output is not redirected on purpose – a closed
            // launcher must not leave it writing into a broken pipe while the game runs.
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(r.GameDll);
        psi.ArgumentList.Add($"{r.Host}:{r.Port}");
        psi.ArgumentList.Add(r.Login.Account);
        psi.ArgumentList.Add(r.Login.Password);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("connect.exe did not start.");
        return Task.FromResult(p.Id);
    }
}
