using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Nostalgia.Launcher.Core;
using Nostalgia.Launcher.Core.Accounts;
using Nostalgia.Launcher.Core.Manifest;
using Nostalgia.Launcher.Core.Platform;
using Nostalgia.Launcher.Core.Quickbars;
using Nostalgia.Launcher.Core.Updates;
using Nostalgia.Launcher.Services;
using Nostalgia.Launcher.ViewModels;
using Nostalgia.Launcher.Views;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Nostalgia.Launcher.Tests.TestAppBuilder))]

namespace Nostalgia.Launcher.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>Fake platform: a valid client folder, recorded launches, a game process that lives as long as told.</summary>
internal sealed class FakePlatform : IPlatformServices, IClientLocator, IGameLauncher, IProcessWatcher, ICredentialStore, IQuickbarIniLocator
{
    public FakePlatform(string clientFolder)
    {
        ClientFolder = clientFolder;
        File.WriteAllBytes(Path.Combine(clientFolder, "game1127.dll"), ClientTests.FakePe(1, 1, 2, 7));
        File.WriteAllBytes(Path.Combine(clientFolder, "connect.exe"), [0]);
    }

    public string ClientFolder { get; }
    public List<GameLaunchRequest> Launches { get; } = [];
    public TimeSpan GameLifetime { get; set; } = TimeSpan.Zero;
    public bool GameAppears { get; set; } = true;
    public LoginCredential? Stored { get; set; }
    public bool ClientRunning { get; set; }
    public string? IniFolder { get; set; }

    public string Name => "fake";
    public bool IsSupported => true;
    public string LauncherExeFileName => "Nostalgia.exe";
    public IClientLocator ClientLocator => this;
    public IGameLauncher GameLauncher => this;
    public IProcessWatcher ProcessWatcher => this;
    public ICredentialStore CredentialStore => this;
    public IQuickbarIniLocator QuickbarIniLocator => this;

    public IReadOnlyList<string> FindCandidateFolders() => [ClientFolder];
    public Task<int> LaunchAsync(GameLaunchRequest request, CancellationToken ct) { Launches.Add(request); return Task.FromResult(4711); }
    public IReadOnlyList<int> FindGameProcesses(string clientFolder, string gameDll) => [];
    public Task<int?> WaitForGameStartAsync(string f, string d, IReadOnlySet<int> ignore, TimeSpan timeout, CancellationToken ct) =>
        Task.FromResult<int?>(GameAppears ? 4712 : null);
    public Task WaitForExitAsync(int pid, CancellationToken ct) => Task.Delay(GameLifetime, ct);
    public bool AnyGameRunning(IReadOnlyList<string> dllNames) => ClientRunning;

    public string Description => "fake";
    public LoginCredential? Load() => Stored;
    public void Save(LoginCredential login) => Stored = login;
    public void Delete() => Stored = null;
    public string? SettingsFolder(string clientFolder) => IniFolder;
}

internal sealed class FakeShell : IUiShell
{
    public string? Copied;
    public List<string> Opened { get; } = [];
    public Task OpenUrlAsync(string url) { Opened.Add(url); return Task.CompletedTask; }
    public Task OpenFolderAsync(string folder) => Task.CompletedTask;
    public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);
    public Task CopyTextAsync(string text) { Copied = text; return Task.CompletedTask; }
    public void RestartInto(string exePath, IReadOnlyList<string> args) { }
}

public class UiTests
{
    private static string ManifestPath(string name) => Path.Combine(AppContext.BaseDirectory, "manifests", name);

    private static MainViewModel CreateVm(string dataDir, FakePlatform platform, string manifest, IQuickbarService? quickbars = null)
    {
        var paths = new AppPaths(dataDir);
        var http = new HttpClient();
        var vm = new MainViewModel(new LauncherContext(paths, platform, quickbars ?? new QuickbarServiceUnavailable(),
            new ManifestService(new HttpManifestFetcher(http), paths.ManifestCache, new LauncherVersion(0, 1, 0)),
            manifest, new UpdateInstaller(http, paths.Updates), new LauncherVersion(0, 1, 0), "test"));
        vm.AttachShell(new FakeShell());
        return vm;
    }

    [AvaloniaFact]
    public async Task Beta_dialog_once_per_notice_version_and_gone_with_phase_null()
    {
        using var data = new TempDir();
        using var client = new TempDir();
        var platform = new FakePlatform(client.Path);

        var vm = CreateVm(data.Path, platform, ManifestPath("local-staging.json"));
        await vm.InitializeAsync();
        Assert.True(vm.HasPhase);
        Assert.Equal("BETA", vm.PhaseBadge);
        Assert.Equal("Nostalgia – PvP Freeshard (Beta Test)", vm.WindowTitle);
        Assert.Equal(Overlay.Beta, vm.CurrentOverlay);
        Assert.Equal(BuiltInDefaults.BetaNoticeEn, vm.PhaseNotice);
        Assert.Equal(["Beta test starting", "Golden Loot Goblin event added", "Rift event added"], vm.News.Select(n => n.Title));
        vm.AcknowledgeBetaCommand.Execute(null);
        Assert.Equal(Overlay.None, vm.CurrentOverlay);

        var again = CreateVm(data.Path, platform, ManifestPath("local-staging.json"));
        await again.InitializeAsync();
        Assert.Equal(Overlay.None, again.CurrentOverlay);

        var v2 = CreateVm(data.Path, platform, ManifestPath("local-notice-v2.json"));
        await v2.InitializeAsync();
        Assert.Equal(Overlay.Beta, v2.CurrentOverlay);

        var none = CreateVm(data.Path, platform, ManifestPath("local-phase-null.json"));
        await none.InitializeAsync();
        Assert.False(none.HasPhase);
        Assert.Equal(Overlay.None, none.CurrentOverlay);
        Assert.Equal("Nostalgia", none.WindowTitle);
        Assert.DoesNotContain("Phase: beta", none.BuildSupportInfo());
    }

    [AvaloniaFact]
    public async Task Play_stores_trimmed_login_launches_and_shows_hint_on_early_exit()
    {
        using var data = new TempDir();
        using var client = new TempDir();
        var platform = new FakePlatform(client.Path) { GameLifetime = TimeSpan.FromMilliseconds(50) };
        var vm = CreateVm(data.Path, platform, ManifestPath("local-staging.json"));
        await vm.InitializeAsync();
        vm.AcknowledgeBetaCommand.Execute(null);

        Assert.False(vm.CanPlay);
        vm.AccountName = "  Bob1\r\n";
        vm.Password = "​Xk7pQ2mZr9TvBn4c  ";
        Assert.Equal("Bob1", vm.AccountName);
        Assert.Equal("Xk7pQ2mZr9TvBn4c", vm.Password);
        Assert.True(vm.CanPlay);

        await vm.PlayCommand.ExecuteAsync(null);
        var launch = Assert.Single(platform.Launches);
        Assert.Equal("127.0.0.1", launch.Host);
        Assert.Equal(10300, launch.Port);
        Assert.Equal("game1127.dll", launch.GameDll);
        Assert.Equal("connect.exe", launch.ConnectTool);
        Assert.Equal(new LoginCredential("Bob1", "Xk7pQ2mZr9TvBn4c"), platform.Stored);
        Assert.Equal(Overlay.LaunchHint, vm.CurrentOverlay);

        // Support info never contains the password.
        string info = vm.BuildSupportInfo();
        Assert.Contains("Bob1", info);
        Assert.DoesNotContain("Xk7pQ2mZr9TvBn4c", info);

        // After /reset: replace the password, play again → stored login replaced.
        vm.ReplacePasswordCommand.Execute(null);
        Assert.Equal("", vm.Password);
        vm.Password = "NewPw9ab";
        await vm.PlayCommand.ExecuteAsync(null);
        Assert.Equal("NewPw9ab", platform.Stored!.Password);

        // Account switch / logout deletes the stored login.
        vm.ForgetLoginCommand.Execute(null);
        Assert.Null(platform.Stored);
        Assert.Equal("", vm.AccountName);
    }

    [AvaloniaFact]
    public async Task No_game_process_shows_the_launch_failed_panel_and_remember_off_stores_nothing()
    {
        using var data = new TempDir();
        using var client = new TempDir();
        var platform = new FakePlatform(client.Path) { GameAppears = false };
        var vm = CreateVm(data.Path, platform, ManifestPath("local-phase-null.json"));
        await vm.InitializeAsync();
        vm.RememberLogin = false;
        vm.AccountName = "Bob1";
        vm.Password = "pw";
        await vm.PlayCommand.ExecuteAsync(null);
        Assert.Equal(Overlay.LaunchFailed, vm.CurrentOverlay);
        Assert.Null(platform.Stored);
    }

    [AvaloniaFact]
    public async Task Stored_login_is_loaded_on_start()
    {
        using var data = new TempDir();
        using var client = new TempDir();
        var platform = new FakePlatform(client.Path) { Stored = new LoginCredential("Alice2", "pw123") };
        var vm = CreateVm(data.Path, platform, ManifestPath("local-phase-null.json"));
        await vm.InitializeAsync();
        Assert.Equal("Alice2", vm.AccountName);
        Assert.Equal("pw123", vm.Password);
        Assert.True(vm.HasStoredLogin);
        Assert.True(vm.CanPlay);
    }

    [AvaloniaFact]
    public async Task Play_runs_the_quickbar_step_first_and_a_running_client_stops_the_start()
    {
        using var data = new TempDir();
        using var client = new TempDir();
        using var inis = new TempDir();
        File.WriteAllText(Path.Combine(inis.Path, "Asdasd-5.ini"), "[Quickbar]\r\nGroupSize=10\r\n");
        var platform = new FakePlatform(client.Path) { IniFolder = inis.Path, ClientRunning = true };
        var api = new FakeQuickbarApi
        {
            Pending = new PendingResponse { FormatVersion = 1, Characters = [QuickbarSamples.Character("Asdasd", "0123456789abcdef0123456789abcdef", (1, 1, 1, 35, 8, "Lunge"))] },
        };
        var vm = CreateVm(data.Path, platform, ManifestPath("local-staging.json"), new QuickbarService(api, platform, platform));
        await vm.InitializeAsync();
        vm.AcknowledgeBetaCommand.Execute(null);
        vm.AccountName = "Bob1";
        vm.Password = "pw";

        await vm.PlayCommand.ExecuteAsync(null);
        Assert.Empty(platform.Launches);
        Assert.Equal("Close the game first, then press Play again.", vm.QuickbarNotice);
        Assert.False(vm.IsLaunching);

        platform.ClientRunning = false;
        await vm.PlayCommand.ExecuteAsync(null);
        Assert.Single(platform.Launches);
        Assert.Equal("Quickbars set up for Asdasd.", vm.QuickbarNotice);
        Assert.Contains("Hotkey_0=35,8,Lunge,0", File.ReadAllText(Path.Combine(inis.Path, "Asdasd-5.ini")));
        Assert.Single(api.Acks);
    }

    [AvaloniaFact]
    public async Task Quickbar_step_is_skipped_when_the_manifest_flag_is_off()
    {
        using var data = new TempDir();
        using var client = new TempDir();
        var platform = new FakePlatform(client.Path) { ClientRunning = true };
        var api = new FakeQuickbarApi();
        var vm = CreateVm(data.Path, platform, ManifestPath("local-phase-null.json"), new QuickbarService(api, platform, platform));
        await vm.InitializeAsync();
        vm.AccountName = "Bob1";
        vm.Password = "pw";
        await vm.PlayCommand.ExecuteAsync(null);
        Assert.Equal(0, api.PendingCalls);
        Assert.Single(platform.Launches);
        Assert.Equal("", vm.QuickbarNotice);
    }

    // ── Screenshots (only when SCREENSHOT_DIR is set) ─────────────────────────────────────

    [AvaloniaFact]
    public async Task Screenshots()
    {
        string? outDir = Environment.GetEnvironmentVariable("SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(outDir)) return;
        Directory.CreateDirectory(outDir);

        using var data = new TempDir();
        using var client = new TempDir();
        var platform = new FakePlatform(client.Path) { Stored = new LoginCredential("Spieler1", "Xk7pQ2mZr9TvBn4c") };

        async Task Shot(string manifest, Action<MainViewModel>? arrange, string name)
        {
            var vm = CreateVm(data.Path, platform, ManifestPath(manifest));
            foreach (var (w, h, suffix) in new[] { (1280, 720, "1280x720"), (1920, 1080, "1920x1080-150pct") })
            {
                var window = new MainWindow { DataContext = vm, Width = w, Height = h };
                window.Show();
                await vm.InitializeAsync();
                vm.ClientFolderText = @"C:\OpenDAoC (game1127.dll, 1.127)"; // no local temp path in published screenshots
                arrange?.Invoke(vm);
                for (int i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(50); }
                var frame = window.CaptureRenderedFrame();
                frame?.Save(Path.Combine(outDir, $"{name}-{suffix}.png"));
                window.Close();
            }
        }

        await Shot("local-staging.json", vm => vm.CurrentOverlay = Overlay.Beta, "01-beta-dialog");
        await Shot("local-staging.json", vm => vm.CurrentOverlay = Overlay.None, "02-main-beta");
        await Shot("local-staging.json", vm => vm.CurrentOverlay = Overlay.LaunchHint, "03-login-refused-hint");
        await Shot("local-staging.json", vm => { vm.CurrentOverlay = Overlay.None; vm.ShowSettingsCommand.Execute(null); }, "04-settings");
        await Shot("local-staging.json", vm => vm.CurrentOverlay = Overlay.RegisterHelp, "05-register-help");
        await Shot("local-phase-null.json", vm => vm.CurrentOverlay = Overlay.None, "06-phase-null");
    }
}

public class UiClickTests
{
    [AvaloniaFact]
    public async Task Clicking_the_play_button_launches()
    {
        using var data = new TempDir();
        using var client = new TempDir();
        var platform = new FakePlatform(client.Path) { GameLifetime = TimeSpan.FromMinutes(5) };
        var paths = new AppPaths(data.Path);
        var http = new HttpClient();
        var vm = new MainViewModel(new LauncherContext(paths, platform, new QuickbarServiceUnavailable(),
            new ManifestService(new HttpManifestFetcher(http), paths.ManifestCache, new LauncherVersion(0, 1, 0)),
            Path.Combine(AppContext.BaseDirectory, "manifests", "local-phase-null.json"), new UpdateInstaller(http, paths.Updates), new LauncherVersion(0, 1, 0), "test"));
        var window = new MainWindow { DataContext = vm, Width = 1280, Height = 720 };
        window.Show();
        await vm.InitializeAsync();
        vm.AccountName = "Bob1";
        vm.Password = "pw";
        Dispatcher.UIThread.RunJobs();
        var button = window.GetVisualDescendants().OfType<Avalonia.Controls.Button>().First(b => b.Classes.Contains("gold") && b is not Avalonia.Controls.Primitives.ToggleButton);
        Assert.True(button.IsEffectivelyEnabled);
        var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(button);
        ((Avalonia.Automation.Provider.IInvokeProvider)peer).Invoke();
        for (int i = 0; i < 10; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Assert.Single(platform.Launches);
    }
}
