using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Nostalgia.Launcher.Core;
using Nostalgia.Launcher.Core.Manifest;
using Nostalgia.Launcher.Core.Quickbars;
using Nostalgia.Launcher.Core.Updates;
using Nostalgia.Launcher.Platform;
using Nostalgia.Launcher.Services;
using Nostalgia.Launcher.ViewModels;
using Serilog;

namespace Nostalgia.Launcher;

internal static class Program
{
    /// <summary>
    /// Arguments:
    ///   --manifest &lt;url|file&gt;   test/staging manifest instead of the baked-in URL (also env NOSTALGIA_MANIFEST)
    ///   --apply-update --target &lt;exe&gt; --pid &lt;n&gt;   internal: self-update step run by the staged new exe
    ///   --updated                internal: started by the update step, removes the staged files
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var paths = AppPaths.Default();
        AppLog.Init(paths.Logs);

        try
        {
            if (args.Contains("--apply-update"))
                return ApplyUpdate(args);

            string manifestLocation = Arg(args, "--manifest")
                                      ?? Environment.GetEnvironmentVariable("NOSTALGIA_MANIFEST")
                                      ?? BuiltInDefaults.ManifestUrl;

            var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"NostalgiaLauncher/{BuildInfo.Version}");
            var platform = PlatformServices.Create(paths);

            App.CreateViewModel = () => new MainViewModel(new LauncherContext(
                paths,
                platform,
                new QuickbarServiceUnavailable(),
                new ManifestService(new HttpManifestFetcher(http), paths.ManifestCache, BuildInfo.Version),
                manifestLocation,
                new UpdateInstaller(http, paths.Updates),
                BuildInfo.Version,
                BuildInfo.ArtVersion));

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Launcher crashed");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();

    private static int ApplyUpdate(string[] args)
    {
        string? target = Arg(args, "--target");
        string? pidText = Arg(args, "--pid");
        string? self = Environment.ProcessPath;
        if (target is null || self is null || !int.TryParse(pidText, out int pid))
        {
            Log.Error("Update step: bad arguments");
            return 2;
        }

        Log.Information("Update step: replacing {Target} with {Self} (waiting for pid {Pid})", target, self, pid);
        try
        {
            UpdateInstaller.ApplyAsync(self, target, pid, TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            Log.Information("Update step: done, starting {Target}", target);
            var psi = new ProcessStartInfo(target) { UseShellExecute = false };
            psi.ArgumentList.Add("--updated");
            Process.Start(psi);
            return 0;
        }
        catch (Exception e)
        {
            Log.Error(e, "Update step failed – starting the old launcher again");
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = false }); } catch (Exception) { }
            return 3;
        }
    }

    private static string? Arg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
