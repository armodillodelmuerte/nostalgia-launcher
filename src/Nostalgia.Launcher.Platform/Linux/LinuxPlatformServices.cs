using Nostalgia.Launcher.Core.Accounts;
using Nostalgia.Launcher.Core.Platform;

namespace Nostalgia.Launcher.Platform.Linux;

/// <summary>
/// Linux stubs. The launcher builds and starts on Linux (manifest, news, status, beta UI), but finding the client,
/// starting it under Wine and storing the login are not implemented yet – see docs/linux-plan.md.
/// </summary>
public sealed class LinuxPlatformServices : IPlatformServices
{
    public const string NotImplemented = "Linux/Wine support is planned – see docs/linux-plan.md.";

    public string Name => "linux";
    public bool IsSupported => false;
    public string LauncherExeFileName => "Nostalgia";
    public IClientLocator ClientLocator { get; } = new WineClientLocator();
    public IGameLauncher GameLauncher { get; } = new WineGameLauncher();
    public IProcessWatcher ProcessWatcher { get; } = new WineProcessWatcher();
    public ICredentialStore CredentialStore { get; } = new SessionOnlyCredentialStore();
    public IQuickbarIniLocator QuickbarIniLocator { get; } = new WineQuickbarIniLocator();
}

/// <summary>Later: Wine prefixes (~/.wine, WINEPREFIX, Lutris, Steam/Proton compatdata) → drive_c/….</summary>
public sealed class WineClientLocator : IClientLocator
{
    public IReadOnlyList<string> FindCandidateFolders() => [];
}

/// <summary>Later: <c>wine connect.exe "&lt;dll&gt;" host:port account password</c> with WINEPREFIX set.</summary>
public sealed class WineGameLauncher : IGameLauncher
{
    public Task<int> LaunchAsync(GameLaunchRequest request, CancellationToken ct) =>
        throw new PlatformNotSupportedException(LinuxPlatformServices.NotImplemented);
}

/// <summary>Later: find the Wine process whose command line names the game dll (/proc/&lt;pid&gt;/cmdline).</summary>
public sealed class WineProcessWatcher : IProcessWatcher
{
    public IReadOnlyList<int> FindGameProcesses(string clientFolder, string gameDll) => [];
    public Task<int?> WaitForGameStartAsync(string clientFolder, string gameDll, IReadOnlySet<int> ignore, TimeSpan timeout, CancellationToken ct) =>
        Task.FromResult<int?>(null);
    public Task WaitForExitAsync(int pid, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Later: libsecret (Secret Service). Until then the login is kept in memory for the session only.</summary>
public sealed class SessionOnlyCredentialStore : ICredentialStore
{
    private LoginCredential? _login;
    public string Description => "nur Sitzung (Linux: libsecret folgt)";
    public LoginCredential? Load() => _login;
    public void Save(LoginCredential login) => _login = login;
    public void Delete() => _login = null;
}

/// <summary>Later: the same path as on Windows inside the Wine prefix (drive_c/users/&lt;user&gt;/AppData/Roaming/…).</summary>
public sealed class WineQuickbarIniLocator : IQuickbarIniLocator
{
    public IReadOnlyList<string> FindCharacterInis() => [];
}
