using Nostalgia.Launcher.Core;
using Nostalgia.Launcher.Core.Platform;
using Nostalgia.Launcher.Platform.Linux;
using Nostalgia.Launcher.Platform.Windows;

namespace Nostalgia.Launcher.Platform;

public static class PlatformServices
{
    public static IPlatformServices Create(AppPaths paths) =>
        OperatingSystem.IsWindows() ? new WindowsPlatformServices(paths) : new LinuxPlatformServices();
}
