using Nostalgia.Launcher.Core.Accounts;

namespace Nostalgia.Launcher.Core.Platform;

/// <summary>Everything that differs between Windows and Linux (Wine). Implementations live in Nostalgia.Launcher.Platform.</summary>
public interface IPlatformServices
{
    /// <summary>"windows", "linux" … (support info).</summary>
    string Name { get; }
    /// <summary>False while the platform is only a stub (Linux today): the UI says so instead of failing later.</summary>
    bool IsSupported { get; }
    /// <summary>File name of the launcher executable for self-update (Nostalgia.exe / Nostalgia).</summary>
    string LauncherExeFileName { get; }
    IClientLocator ClientLocator { get; }
    IGameLauncher GameLauncher { get; }
    IProcessWatcher ProcessWatcher { get; }
    ICredentialStore CredentialStore { get; }
    IQuickbarIniLocator QuickbarIniLocator { get; }
}

/// <summary>Finds candidate client folders (registry, default paths). Validation is platform-free: <see cref="Client.ClientValidator"/>.</summary>
public interface IClientLocator
{
    IReadOnlyList<string> FindCandidateFolders();
}

public sealed record GameLaunchRequest(string ClientFolder, string ConnectTool, string GameDll, string Host, int Port, LoginCredential Login);

/// <summary>Starts the client. Windows: <c>connect.exe "&lt;game dll&gt;" host:port account password</c> from the client folder.</summary>
public interface IGameLauncher
{
    /// <summary>Starts the connect tool; returns its process id. Never logs the password.</summary>
    Task<int> LaunchAsync(GameLaunchRequest request, CancellationToken ct);
}

/// <summary>Finds and watches running game processes (the game dll started by the connect tool).</summary>
public interface IProcessWatcher
{
    IReadOnlyList<int> FindGameProcesses(string clientFolder, string gameDll);
    /// <summary>Waits until a new game process appears (ids in <paramref name="ignore"/> existed before the launch).</summary>
    Task<int?> WaitForGameStartAsync(string clientFolder, string gameDll, IReadOnlySet<int> ignore, TimeSpan timeout, CancellationToken ct);
    Task WaitForExitAsync(int pid, CancellationToken ct);
}

/// <summary>Stores the login encrypted for the current OS user (Windows: DPAPI; Linux later: libsecret).</summary>
public interface ICredentialStore
{
    string Description { get; }
    LoginCredential? Load();
    void Save(LoginCredential login);
    void Delete();
}

/// <summary>Finds the client's character INI files (quickbar import; see docs/client/quickbar-ini.md once calibrated).</summary>
public interface IQuickbarIniLocator
{
    IReadOnlyList<string> FindCharacterInis();
}
