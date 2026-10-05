using System.Runtime.Versioning;
using Nostalgia.Launcher.Core;
using Nostalgia.Launcher.Core.Platform;

namespace Nostalgia.Launcher.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformServices(AppPaths paths) : IPlatformServices
{
    public string Name => "windows";
    public bool IsSupported => true;
    public string LauncherExeFileName => "Nostalgia.exe";
    public IClientLocator ClientLocator { get; } = new WindowsClientLocator();
    public IGameLauncher GameLauncher { get; } = new WindowsGameLauncher();
    public IProcessWatcher ProcessWatcher { get; } = new WindowsProcessWatcher();
    public ICredentialStore CredentialStore { get; } = new DpapiCredentialStore(paths.Login);
    public IQuickbarIniLocator QuickbarIniLocator { get; } = new WindowsQuickbarIniLocator();
}
