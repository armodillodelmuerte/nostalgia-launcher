using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nostalgia.Launcher.Core;
using Nostalgia.Launcher.Core.Accounts;
using Nostalgia.Launcher.Core.Client;
using Nostalgia.Launcher.Core.Manifest;
using PhaseRules = Nostalgia.Launcher.Core.Phase.PhaseNotice;
using Nostalgia.Launcher.Core.Platform;
using Nostalgia.Launcher.Core.Quickbars;
using Nostalgia.Launcher.Core.Settings;
using Nostalgia.Launcher.Core.Status;
using Nostalgia.Launcher.Core.Support;
using Nostalgia.Launcher.Core.Updates;
using Nostalgia.Launcher.Resources;
using Nostalgia.Launcher.Services;
using Serilog;

namespace Nostalgia.Launcher.ViewModels;

public enum Page { Play, Quickbars, Settings }

public enum Overlay { None, Beta, RegisterHelp, ResetHelp, LaunchHint, LaunchFailed, About, Privacy }

public sealed record LauncherContext(
    AppPaths Paths,
    IPlatformServices Platform,
    IQuickbarService Quickbars,
    ManifestService Manifests,
    string ManifestLocation,
    UpdateInstaller Updates,
    LauncherVersion Version,
    string ArtVersion);

public sealed partial class MainViewModel : ObservableObject
{
    private readonly LauncherContext _ctx;
    private readonly SettingsStore _settingsStore;
    private LauncherSettings _settings;
    private IUiShell? _shell;
    private LoginCredential? _storedLogin;
    private ClientCheck? _client;
    private UpdatePlan? _updatePlan;
    private ManifestLoadResult? _manifestLoad;
    private ServerStatus? _status;
    private CancellationTokenSource? _statusLoop;

    public MainViewModel(LauncherContext ctx)
    {
        _ctx = ctx;
        _settingsStore = new SettingsStore(ctx.Paths.Settings);
        _settings = _settingsStore.Load();
        _rememberLogin = _settings.RememberLogin;
        Manifest = BuiltInDefaults.Fallback(ctx.Version);
        ApplyManifest();
    }

    public void AttachShell(IUiShell shell) => _shell = shell;

    // ── Manifest-driven state ────────────────────────────────────────────────────────────────

    [ObservableProperty] private LauncherManifest _manifest;
    public ObservableCollection<NewsItemViewModel> News { get; } = [];

    [ObservableProperty] private string _windowTitle = Strings.WindowTitlePlain;
    [ObservableProperty] private bool _hasPhase;
    [ObservableProperty] private string _phaseBadge = "";
    [ObservableProperty] private string _phaseTitle = "";
    [ObservableProperty] private string _phaseNotice = "";
    [ObservableProperty] private bool _quickbarsVisible;

    public string StripText => Strings.BetaStrip;
    public string FooterLine => string.Format(CultureInfo.CurrentCulture, Strings.FooterLine, _ctx.Version,
        _client is { Ok: true } c ? string.Format(CultureInfo.CurrentCulture, Strings.FooterClientFound, c.VersionText) : Strings.FooterClientMissing);
    public string Disclaimer => Strings.DisclaimerShort;
    public string RegisterCommand => Manifest.Bot.RegisterCommand;
    public string ResetCommand => Manifest.Bot.ResetCommand;
    public string RegisterSteps => string.Format(CultureInfo.CurrentCulture, Strings.RegisterSteps, Manifest.Bot.RegisterCommand);
    public string ResetSteps => string.Format(CultureInfo.CurrentCulture, Strings.ResetSteps, Manifest.Bot.ResetCommand);
    public bool HasFeedbackLink => !string.IsNullOrWhiteSpace(Manifest.Links.FeedbackChannel);
    public bool HasDiscordLink => !string.IsNullOrWhiteSpace(Manifest.Links.DiscordInvite);
    public bool HasWebsite => !string.IsNullOrWhiteSpace(Manifest.Links.Website);
    public bool HasNews => News.Count > 0;

    private void ApplyManifest()
    {
        var phase = Manifest.Phase;
        HasPhase = PhaseRules.HasPhaseUi(phase);
        PhaseBadge = phase?.Badge ?? "";
        PhaseTitle = phase?.Title ?? "";
        PhaseNotice = phase?.NoticeEn ?? "";
        WindowTitle = PhaseRules.WindowTitle(phase, Strings.WindowTitleBeta, Strings.WindowTitlePlain);
        QuickbarsVisible = QuickbarFeature.Visible(Manifest.Features.Quickbars, _ctx.Quickbars);
        if (!QuickbarsVisible && CurrentPage == Page.Quickbars) CurrentPage = Page.Play;

        News.Clear();
        foreach (var n in Manifest.News.OrderByDescending(n => n.ParsedDate ?? DateOnly.MinValue).Take(8))
            News.Add(new NewsItemViewModel(n));

        _updatePlan = UpdatePlanner.Plan(_ctx.Version, Manifest.Launcher);
        UpdateRequired = _updatePlan.Kind is UpdateKind.Required or UpdateKind.RequiredUnavailable;
        UpdateAvailable = _updatePlan.Kind is UpdateKind.Optional or UpdateKind.Required;
        UpdateText = _updatePlan.Kind switch
        {
            UpdateKind.Required => string.Format(CultureInfo.CurrentCulture, Strings.UpdateRequired, _updatePlan.Target),
            UpdateKind.Optional => string.Format(CultureInfo.CurrentCulture, Strings.UpdateAvailable, _updatePlan.Target),
            UpdateKind.RequiredUnavailable => Strings.UpdateRequiredUnavailable,
            _ => "",
        };

        foreach (var name in new[] { nameof(RegisterCommand), nameof(ResetCommand), nameof(RegisterSteps), nameof(ResetSteps),
                     nameof(HasFeedbackLink), nameof(HasDiscordLink), nameof(HasWebsite), nameof(HasNews), nameof(FooterLine) })
            OnPropertyChanged(name);
        RefreshPlayState();
    }

    // ── Startup ──────────────────────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        Log.Information("Launcher {Version} (art {Art}) on {Platform}, data {Data}", _ctx.Version, _ctx.ArtVersion, _ctx.Platform.Name, _ctx.Paths.Root);

        try { _storedLogin = _ctx.Platform.CredentialStore.Load(); }
        catch (Exception e) { Log.Warning(e, "Could not read the stored login"); }
        if (_storedLogin is not null)
        {
            _accountName = _storedLogin.Account;
            _password = _storedLogin.Password;
            OnPropertyChanged(nameof(AccountName));
            OnPropertyChanged(nameof(Password));
            Log.Information("Stored login for account {Account}", _storedLogin.Account);
        }
        HasStoredLogin = _storedLogin is not null;

        _manifestLoad = await _ctx.Manifests.LoadAsync(_ctx.ManifestLocation);
        if (_manifestLoad.RemoteError is not null)
            Log.Warning("Manifest {Location}: {Error} – using {Source}", _ctx.ManifestLocation, _manifestLoad.RemoteError, _manifestLoad.Source);
        else
            Log.Information("Manifest loaded from {Location}", _manifestLoad.Location);
        Manifest = _manifestLoad.Manifest;
        ManifestOffline = _manifestLoad.Source != ManifestSource.Remote;
        ApplyManifest();

        CheckClient();
        StartStatusLoop();
        ShowBetaIfDue();

        if (_ctx.Updates is not null && Environment.GetCommandLineArgs().Contains("--updated"))
            _ = _ctx.Updates.CleanupAsync();
    }

    private void ShowBetaIfDue()
    {
        // Order on first start: client → beta notice → account screen.
        if (ClientSetupNeeded) return;
        if (PhaseRules.ShouldShowDialog(Manifest.Phase, _settings))
            CurrentOverlay = Overlay.Beta;
    }

    [RelayCommand]
    private void AcknowledgeBeta()
    {
        if (Manifest.Phase is { } phase)
        {
            PhaseRules.Acknowledge(phase, _settings);
            SaveSettings();
            Log.Information("Phase notice {Phase} v{Version} acknowledged", phase.Id, phase.NoticeVersion);
        }
        CurrentOverlay = Overlay.None;
    }

    // ── Client ───────────────────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool _clientSetupNeeded;
    [ObservableProperty] private string _clientMessage = "";
    [ObservableProperty] private string _clientFolderText = "";

    public bool PlatformSupported => _ctx.Platform.IsSupported;

    private void CheckClient()
    {
        var folders = new List<string>();
        if (!string.IsNullOrWhiteSpace(_settings.ClientFolder)) folders.Add(_settings.ClientFolder);
        try { folders.AddRange(_ctx.Platform.ClientLocator.FindCandidateFolders()); }
        catch (Exception e) { Log.Warning(e, "Client search failed"); }
        UseClientCheck(ClientValidator.FindFirst(folders, Manifest.Client, _settings.ClientDll), fromPick: false);
    }

    private void UseClientCheck(ClientCheck check, bool fromPick)
    {
        _client = check;
        Log.Information("Client check {Folder}: {Status} {Dll} {Version}", check.Folder, check.Status, check.GameDll, check.VersionText);
        if (check.Ok)
        {
            _settings.ClientFolder = check.Folder;
            _settings.ClientDll = check.GameDll;
            SaveSettings();
        }

        ClientSetupNeeded = !check.Ok;
        ClientFolderText = check.Ok ? $"{check.Folder} ({check.GameDll}, {check.VersionText})" : Strings.ClientNotSet;
        string supported = string.Join(", ", Manifest.Client.SupportedVersions.Select(PeVersionReader.ToClientString));
        ClientMessage = !_ctx.Platform.IsSupported ? Strings.PlatformNotSupported
            : check.Status switch
            {
                ClientStatus.Ok => "",
                ClientStatus.FolderMissing => fromPick ? Strings.ClientFolderMissing : Strings.ClientNotFound,
                ClientStatus.NoGameDll => string.Format(CultureInfo.CurrentCulture, Strings.ClientNoGameDll, check.Folder),
                ClientStatus.UnsupportedVersion => string.Format(CultureInfo.CurrentCulture, Strings.ClientUnsupported,
                    check.GameDll, check.VersionText ?? "?", supported),
                ClientStatus.NoConnectTool => string.Format(CultureInfo.CurrentCulture, Strings.ClientNoConnect, ClientValidator.ConnectTool),
                _ => "",
            };
        OnPropertyChanged(nameof(FooterLine));
        RefreshPlayState();
    }

    [RelayCommand]
    private async Task PickClientFolder()
    {
        if (_shell is null) return;
        string? folder = await _shell.PickFolderAsync(Strings.PickClientTitle);
        if (folder is null) return;
        UseClientCheck(ClientValidator.Check(folder, Manifest.Client), fromPick: true);
        if (_client is { Ok: true }) ShowBetaIfDue();
    }

    [RelayCommand]
    private void RetryClientSearch()
    {
        CheckClient();
        if (_client is { Ok: true }) ShowBetaIfDue();
    }

    [RelayCommand]
    private Task OpenClientInfo() => OpenUrl(Manifest.Client.InfoUrl ?? "https://www.opendaoc.com/docs/client/");

    // ── Account ──────────────────────────────────────────────────────────────────────────────

    private string _accountName = "";
    private string _password = "";

    /// <summary>Pasted values are trimmed (spaces, line breaks, zero-width characters at both ends).</summary>
    public string AccountName
    {
        get => _accountName;
        set
        {
            if (SetProperty(ref _accountName, PasteSanitizer.Clean(value))) { LoginError = ""; RefreshPlayState(); }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, PasteSanitizer.Clean(value))) { LoginError = ""; RefreshPlayState(); }
        }
    }

    [ObservableProperty] private bool _showPassword;
    [ObservableProperty] private bool _rememberLogin = true;
    [ObservableProperty] private bool _hasStoredLogin;
    [ObservableProperty] private string _loginError = "";

    public char PasswordChar => ShowPassword ? '\0' : '•';
    public string ShowPasswordText => ShowPassword ? Strings.HidePassword : Strings.ShowPassword;
    partial void OnShowPasswordChanged(bool value)
    {
        OnPropertyChanged(nameof(PasswordChar));
        OnPropertyChanged(nameof(ShowPasswordText));
    }

    partial void OnRememberLoginChanged(bool value)
    {
        _settings.RememberLogin = value;
        SaveSettings();
        if (!value && _storedLogin is not null)
        {
            // Unticked: forget the stored copy right away (the fields keep the values for this session).
            DeleteStoredLogin();
        }
    }

    [RelayCommand] private void ToggleShowPassword() => ShowPassword = !ShowPassword;

    [RelayCommand]
    private void ForgetLogin()
    {
        DeleteStoredLogin();
        AccountName = "";
        Password = "";
        ShowPassword = false;
        CurrentPage = Page.Play;
        Log.Information("Login forgotten (account switch / logout)");
    }

    private void DeleteStoredLogin()
    {
        try { _ctx.Platform.CredentialStore.Delete(); }
        catch (Exception e) { Log.Warning(e, "Could not delete the stored login"); }
        _storedLogin = null;
        HasStoredLogin = false;
    }

    // ── Play ─────────────────────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool _isLaunching;
    [ObservableProperty] private bool _gameRunning;
    [ObservableProperty] private string _playHint = "";
    [ObservableProperty] private bool _canPlay;

    private void RefreshPlayState()
    {
        CanPlay = !IsLaunching && !UpdateRequired && !IsUpdating && _client is { Ok: true } && _ctx.Platform.IsSupported
                  && !string.IsNullOrWhiteSpace(Manifest.Server.Host)
                  && AccountName.Length > 0 && Password.Length > 0;
        PlayHint = UpdateRequired ? Strings.PlayBlockedUpdate
            : string.IsNullOrWhiteSpace(Manifest.Server.Host) ? Strings.PlayBlockedNoManifest
            : IsLaunching ? Strings.PlayStarting
            : GameRunning ? Strings.PlayRunning
            : "";
    }

    partial void OnIsLaunchingChanged(bool value) => RefreshPlayState();
    partial void OnGameRunningChanged(bool value) => RefreshPlayState();

    [RelayCommand]
    private async Task Play()
    {
        if (!CanPlay || _client is not { Ok: true } client) return;
        if (!LoginCredential.IsValidAccountName(AccountName))
        {
            LoginError = Strings.AccountNameInvalid;
            return;
        }

        var login = new LoginCredential(AccountName, Password);
        if (RememberLogin)
        {
            try
            {
                if (_storedLogin != login)
                {
                    _ctx.Platform.CredentialStore.Save(login);
                    Log.Information("Login stored for account {Account}{Replaced}", login.Account, _storedLogin is null ? "" : " (replaced)");
                }
                _storedLogin = login;
                HasStoredLogin = true;
            }
            catch (Exception e) { Log.Warning(e, "Could not store the login"); }
        }

        IsLaunching = true;
        var server = Manifest.Server;
        var before = _ctx.Platform.ProcessWatcher.FindGameProcesses(client.Folder, client.GameDll!).ToHashSet();
        try
        {
            Log.Information("Play: account {Account} → {Host}:{Port} via {Tool} {Dll} in {Folder}",
                login.Account, server.Host, server.LoginPort, ClientValidator.ConnectTool, client.GameDll, client.Folder);
            await _ctx.Platform.GameLauncher.LaunchAsync(
                new GameLaunchRequest(client.Folder, ClientValidator.ConnectTool, client.GameDll!, server.Host, server.LoginPort, login),
                CancellationToken.None);

            int? pid = await _ctx.Platform.ProcessWatcher.WaitForGameStartAsync(client.Folder, client.GameDll!, before,
                LaunchAssessment.StartTimeout, CancellationToken.None);
            IsLaunching = false;
            if (pid is null)
            {
                Log.Warning("Play: no game process within {Seconds} s", LaunchAssessment.StartTimeout.TotalSeconds);
                CurrentOverlay = Overlay.LaunchFailed;
                return;
            }

            GameRunning = true;
            var started = DateTime.UtcNow;
            Log.Information("Play: game process {Pid} started", pid);
            await _ctx.Platform.ProcessWatcher.WaitForExitAsync(pid.Value, CancellationToken.None);
            var runtime = DateTime.UtcNow - started;
            GameRunning = _ctx.Platform.ProcessWatcher.FindGameProcesses(client.Folder, client.GameDll!).Count > 0;
            var outcome = LaunchAssessment.Assess(true, runtime);
            Log.Information("Play: game process {Pid} ended after {Seconds:0} s → {Outcome}", pid, runtime.TotalSeconds, outcome);
            if (outcome == LaunchOutcome.EarlyExit)
            {
                await RefreshStatusAsync();
                CurrentOverlay = Overlay.LaunchHint;
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Play: start failed");
            CurrentOverlay = Overlay.LaunchFailed;
        }
        finally
        {
            IsLaunching = false;
        }
    }

    public string HintWrongPasswordLine => string.Format(CultureInfo.CurrentCulture, Strings.HintWrongPassword, Manifest.Bot.ResetCommand);

    public string LaunchHintServerLine => _status is { Online: false } ? Strings.HintServerOfflineNow : Strings.HintServerOffline;

    // ── Server status ────────────────────────────────────────────────────────────────────────

    [ObservableProperty] private string _statusText = Strings.StatusChecking;
    [ObservableProperty] private bool? _serverOnline;
    [ObservableProperty] private bool _manifestOffline;

    private void StartStatusLoop()
    {
        _statusLoop?.Cancel();
        _statusLoop = new CancellationTokenSource();
        var ct = _statusLoop.Token;
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await RefreshStatusAsync();
                try { await Task.Delay(TimeSpan.FromSeconds(30), ct); } catch (OperationCanceledException) { }
            }
        }, ct);
    }

    private async Task RefreshStatusAsync()
    {
        var s = Manifest.Server;
        var result = await ServerStatusProbe.ProbeAsync(s.Host, s.LoginPort, TimeSpan.FromSeconds(4));
        if (_status?.Online != result.Online)
            Log.Information("Server {Host}:{Port} {State} {Latency}", s.Host, s.LoginPort, result.Online ? "online" : "offline", (object?)result.LatencyMs ?? result.Error ?? "");
        _status = result;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ServerOnline = string.IsNullOrWhiteSpace(s.Host) ? null : result.Online;
            StatusText = string.IsNullOrWhiteSpace(s.Host) ? Strings.StatusUnknown
                : result.Online ? string.Format(CultureInfo.CurrentCulture, Strings.StatusOnline, result.LatencyMs)
                : Strings.StatusOffline;
            OnPropertyChanged(nameof(LaunchHintServerLine));
        });
    }

    // ── Update ───────────────────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private bool _updateRequired;
    [ObservableProperty] private string _updateText = "";
    [ObservableProperty] private bool _isUpdating;
    [ObservableProperty] private double _updateProgress;
    [ObservableProperty] private string _updateError = "";

    public bool ShowUpdateBanner => UpdateAvailable || UpdateRequired;
    partial void OnUpdateAvailableChanged(bool value) => OnPropertyChanged(nameof(ShowUpdateBanner));
    partial void OnUpdateRequiredChanged(bool value) { OnPropertyChanged(nameof(ShowUpdateBanner)); RefreshPlayState(); }
    partial void OnIsUpdatingChanged(bool value) => RefreshPlayState();

    [RelayCommand]
    private async Task StartUpdate()
    {
        if (_updatePlan is not { Target: not null } plan || _shell is null || IsUpdating) return;
        string? self = Environment.ProcessPath;
        if (self is null) return;
        IsUpdating = true;
        UpdateError = "";
        try
        {
            Log.Information("Update {From} → {To} from {Url}", plan.Current, plan.Target, plan.DownloadUrl);
            var progress = new Progress<double>(p => UpdateProgress = p);
            string staged = await _ctx.Updates.StageAsync(plan, _ctx.Platform.LauncherExeFileName, progress, CancellationToken.None);
            Log.Information("Update {To} downloaded and verified (SHA-256), restarting", plan.Target);
            await Log.CloseAndFlushAsync();
            _shell.RestartInto(staged, ["--apply-update", "--target", self, "--pid", Environment.ProcessId.ToString(CultureInfo.InvariantCulture)]);
        }
        catch (UpdateVerificationException e)
        {
            Log.Error(e, "Update rejected");
            UpdateError = Strings.UpdateHashMismatch;
        }
        catch (Exception e)
        {
            Log.Error(e, "Update failed");
            UpdateError = string.Format(CultureInfo.CurrentCulture, Strings.UpdateFailed, e.Message);
        }
        finally
        {
            IsUpdating = false;
        }
    }

    // ── Navigation, overlays, links ──────────────────────────────────────────────────────────

    [ObservableProperty] private Page _currentPage = Page.Play;
    [ObservableProperty] private Overlay _currentOverlay = Overlay.None;
    [ObservableProperty] private string _toast = "";

    public bool IsPlayPage => CurrentPage == Page.Play;
    public bool IsSettingsPage => CurrentPage == Page.Settings;
    public bool IsQuickbarsPage => CurrentPage == Page.Quickbars;
    partial void OnCurrentPageChanged(Page value)
    {
        OnPropertyChanged(nameof(IsPlayPage));
        OnPropertyChanged(nameof(IsSettingsPage));
        OnPropertyChanged(nameof(IsQuickbarsPage));
    }

    public bool OverlayVisible => CurrentOverlay != Overlay.None;
    public bool IsBetaOverlay => CurrentOverlay == Overlay.Beta;
    public bool IsRegisterOverlay => CurrentOverlay == Overlay.RegisterHelp;
    public bool IsResetOverlay => CurrentOverlay == Overlay.ResetHelp;
    public bool IsLaunchHintOverlay => CurrentOverlay == Overlay.LaunchHint;
    public bool IsLaunchFailedOverlay => CurrentOverlay == Overlay.LaunchFailed;
    public bool IsAboutOverlay => CurrentOverlay == Overlay.About;
    public bool IsPrivacyOverlay => CurrentOverlay == Overlay.Privacy;
    partial void OnCurrentOverlayChanged(Overlay value)
    {
        foreach (var n in new[] { nameof(OverlayVisible), nameof(IsBetaOverlay), nameof(IsRegisterOverlay), nameof(IsResetOverlay),
                     nameof(IsLaunchHintOverlay), nameof(IsLaunchFailedOverlay), nameof(IsAboutOverlay), nameof(IsPrivacyOverlay) })
            OnPropertyChanged(n);
    }

    [RelayCommand] private void ShowPlay() => CurrentPage = Page.Play;
    [RelayCommand] private void ShowSettings() => CurrentPage = Page.Settings;
    [RelayCommand] private void ShowQuickbars() { if (QuickbarsVisible) CurrentPage = Page.Quickbars; }
    [RelayCommand] private void ShowRegisterHelp() => CurrentOverlay = Overlay.RegisterHelp;
    [RelayCommand] private void ShowResetHelp() => CurrentOverlay = Overlay.ResetHelp;
    [RelayCommand] private void ShowAbout() => CurrentOverlay = Overlay.About;
    [RelayCommand] private void ShowPrivacy() => CurrentOverlay = Overlay.Privacy;
    [RelayCommand] private void ShowBetaNotice() { if (HasPhase) CurrentOverlay = Overlay.Beta; }
    [RelayCommand] private void CloseOverlay() => CurrentOverlay = Overlay.None;

    /// <summary>From the reset help / hint panel: focus the password field for the new password.</summary>
    [RelayCommand]
    private void ReplacePassword()
    {
        Password = "";
        ShowPassword = true;
        CurrentPage = Page.Play;
        CurrentOverlay = Overlay.None;
        PasswordFocusRequested?.Invoke();
    }

    public event Action? PasswordFocusRequested;

    [RelayCommand] private Task OpenDiscord() => OpenUrl(Manifest.Links.DiscordInvite);
    [RelayCommand] private Task OpenFeedback() => OpenUrl(Manifest.Links.FeedbackChannel ?? Manifest.Links.DiscordInvite);
    [RelayCommand] private Task OpenWebsite() => OpenUrl(Manifest.Links.Website);
    [RelayCommand] private Task OpenSource() => OpenUrl(BuildInfo.RepoUrl);
    [RelayCommand] private Task OpenNews(NewsItemViewModel? item) => OpenUrl(item?.Link);

    [RelayCommand]
    private async Task OpenLogs()
    {
        if (_shell is null) return;
        Directory.CreateDirectory(_ctx.Paths.Logs);
        await _shell.OpenFolderAsync(_ctx.Paths.Logs);
    }

    private async Task OpenUrl(string? url)
    {
        if (_shell is null || string.IsNullOrWhiteSpace(url)) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            Log.Warning("Refused to open non-web link {Url}", url);
            return;
        }
        await _shell.OpenUrlAsync(url);
    }

    [RelayCommand]
    private async Task CopySupportInfo()
    {
        if (_shell is null) return;
        await _shell.CopyTextAsync(BuildSupportInfo());
        Toast = Strings.SupportCopied;
        _ = Task.Delay(3000).ContinueWith(_ => Avalonia.Threading.Dispatcher.UIThread.Post(() => Toast = ""));
    }

    public string BuildSupportInfo() => SupportInfo.Build(new SupportInfoData
    {
        LauncherVersion = _ctx.Version.ToString(),
        ArtVersion = _ctx.ArtVersion,
        Platform = _ctx.Platform.Name,
        OsDescription = RuntimeInformation.OSDescription,
        PhaseId = Manifest.Phase?.Id,
        PhaseNoticeVersion = Manifest.Phase?.NoticeVersion,
        ManifestSource = _manifestLoad is null ? "not loaded" : $"{_manifestLoad.Source} ({_manifestLoad.Location})",
        ManifestError = _manifestLoad?.RemoteError,
        ServerHost = Manifest.Server.Host,
        ServerPort = Manifest.Server.LoginPort,
        ServerStatus = _status is null ? null : _status.Online ? $"online {_status.LatencyMs} ms" : $"offline ({_status.Error})",
        ClientFolder = _client?.Folder,
        ClientDll = _client?.GameDll,
        ClientVersion = _client?.VersionText,
        ClientStatus = _client?.Status.ToString(),
        AccountName = string.IsNullOrEmpty(AccountName) ? null : AccountName,
        CredentialStore = _ctx.Platform.CredentialStore.Description + (HasStoredLogin ? ", stored" : ", not stored"),
        DataFolder = _ctx.Paths.Root,
        LogFolder = _ctx.Paths.Logs,
        RecentErrors = AppLog.Recent.Snapshot(),
    }, DateTimeOffset.Now);

    // ── About / privacy texts ────────────────────────────────────────────────────────────────

    public string AboutVersionLine => string.Format(CultureInfo.CurrentCulture, Strings.AboutVersion, _ctx.Version, _ctx.ArtVersion,
        Manifest.Phase is { } p ? $"{p.Id} (v{p.NoticeVersion})" : Strings.AboutNoPhase);
    public string LicenseCinzel => SafeAsset("Assets/Brand/Fonts/OFL-Cinzel.txt");
    public string LicenseInter => SafeAsset("Assets/Brand/Fonts/OFL-Inter.txt");
    public string PrivacyText => string.Format(CultureInfo.CurrentCulture, Strings.PrivacyText, _ctx.Paths.Root);

    private static string SafeAsset(string path)
    {
        try { return BuildInfo.ReadAsset(path); } catch (Exception) { return ""; }
    }

    partial void OnManifestChanged(LauncherManifest value) => OnPropertyChanged(nameof(AboutVersionLine));

    private void SaveSettings()
    {
        try { _settingsStore.Save(_settings); }
        catch (Exception e) { Log.Warning(e, "Could not save settings"); }
    }
}

public sealed class NewsItemViewModel(NewsItem item)
{
    public string Title => item.Title;
    public string Text => item.Text;
    public string? Link => item.Link;
    public bool HasLink => !string.IsNullOrWhiteSpace(item.Link);
    public string DateText => item.ParsedDate is { } d ? d.ToString("MMM d", CultureInfo.GetCultureInfo("en-US")) : item.Date;
}
