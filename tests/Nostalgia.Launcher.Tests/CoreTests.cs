using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using Nostalgia.Launcher.Core;
using Nostalgia.Launcher.Core.Accounts;
using Nostalgia.Launcher.Core.Client;
using Nostalgia.Launcher.Core.Manifest;
using Nostalgia.Launcher.Core.Phase;
using Nostalgia.Launcher.Core.Quickbars;
using Nostalgia.Launcher.Core.Settings;
using Nostalgia.Launcher.Core.Support;
using Nostalgia.Launcher.Core.Updates;
using Xunit;

namespace Nostalgia.Launcher.Tests;

public class PhaseNoticeTests
{
    private static PhaseInfo Beta(int v) => BuiltInDefaults.BetaPhase with { NoticeVersion = v };

    [Fact]
    public void Dialog_shows_once_per_notice_version()
    {
        var s = new LauncherSettings();
        Assert.True(PhaseNotice.ShouldShowDialog(Beta(1), s));
        PhaseNotice.Acknowledge(Beta(1), s);
        Assert.False(PhaseNotice.ShouldShowDialog(Beta(1), s));
        Assert.True(PhaseNotice.ShouldShowDialog(Beta(2), s));
        PhaseNotice.Acknowledge(Beta(2), s);
        Assert.False(PhaseNotice.ShouldShowDialog(Beta(2), s));
        // Going back down never re-shows and never lowers the stored value.
        PhaseNotice.Acknowledge(Beta(1), s);
        Assert.Equal(2, s.AcknowledgedNotices["beta"]);
    }

    [Fact]
    public void Acknowledgement_survives_a_restart()
    {
        using var tmp = new TempDir();
        var store = new SettingsStore(Path.Combine(tmp.Path, "settings.json"));
        var s = store.Load();
        PhaseNotice.Acknowledge(Beta(1), s);
        store.Save(s);
        Assert.False(PhaseNotice.ShouldShowDialog(Beta(1), store.Load()));
        Assert.True(PhaseNotice.ShouldShowDialog(Beta(2), store.Load()));
    }

    [Fact]
    public void No_phase_no_dialog_and_phases_are_independent()
    {
        var s = new LauncherSettings();
        Assert.False(PhaseNotice.ShouldShowDialog(null, s));
        Assert.False(PhaseNotice.HasPhaseUi(null));
        PhaseNotice.Acknowledge(Beta(1), s);
        Assert.True(PhaseNotice.ShouldShowDialog(new PhaseInfo { Id = "launch", NoticeVersion = 1 }, s));
    }

    [Fact]
    public void Window_title_follows_the_phase()
    {
        Assert.Equal("B", PhaseNotice.WindowTitle(Beta(1), "B", "P"));
        Assert.Equal("P", PhaseNotice.WindowTitle(null, "B", "P"));
        Assert.Equal("P – Launch-Woche", PhaseNotice.WindowTitle(new PhaseInfo { Id = "launch", Title = "Launch-Woche" }, "B", "P"));
    }
}

public class VersionTests
{
    [Theory]
    [InlineData("0.1.0", "0.1.1", -1)]
    [InlineData("0.1.10", "0.1.9", 1)]
    [InlineData("1.0.0", "0.9.9", 1)]
    [InlineData("v0.2.0", "0.2.0", 0)]
    [InlineData("0.2.0-rc1", "0.2.0", -1)]
    [InlineData("0.2.0-rc2", "0.2.0-rc1", 1)]
    [InlineData("0.1.0.0", "0.1.0", 0)]
    [InlineData("0.1.0+abc", "0.1.0", 0)]
    [InlineData("0.1", "0.1.0", 0)]
    public void Compares(string a, string b, int expected) =>
        Assert.Equal(expected, Math.Sign(LauncherVersion.Parse(a).CompareTo(LauncherVersion.Parse(b))));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1.-2.3")]
    [InlineData("1.2.3-")]
    public void Rejects_garbage(string s) => Assert.False(LauncherVersion.TryParse(s, out _));
}

public class UpdateTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly LauncherVersion V010 = new(0, 1, 0);

    private static LauncherRelease Rel(string min, string latest, string? url = "https://h/Nostalgia.exe", string? sha = Sha) =>
        new() { MinimumVersion = min, LatestVersion = latest, DownloadUrl = url, Sha256 = sha };

    [Fact] public void Up_to_date() => Assert.Equal(UpdateKind.None, UpdatePlanner.Plan(V010, Rel("0.1.0", "0.1.0")).Kind);
    [Fact] public void Optional_update() => Assert.Equal(UpdateKind.Optional, UpdatePlanner.Plan(V010, Rel("0.1.0", "0.1.1")).Kind);
    [Fact] public void Required_update() => Assert.Equal(UpdateKind.Required, UpdatePlanner.Plan(V010, Rel("0.1.1", "0.1.1")).Kind);
    [Fact] public void Never_downgrades() => Assert.Equal(UpdateKind.None, UpdatePlanner.Plan(new(0, 2, 0), Rel("0.1.0", "0.1.5")).Kind);
    [Fact] public void Required_without_newer_download() => Assert.Equal(UpdateKind.RequiredUnavailable, UpdatePlanner.Plan(V010, Rel("0.2.0", "0.1.0")).Kind);
    [Fact] public void No_update_without_hash() => Assert.Equal(UpdateKind.None, UpdatePlanner.Plan(V010, Rel("0.1.0", "0.1.1", sha: null)).Kind);
    [Fact] public void No_update_over_plain_http() => Assert.Equal(UpdateKind.None, UpdatePlanner.Plan(V010, Rel("0.1.0", "0.1.1", url: "http://example.org/N.exe")).Kind);
    [Fact] public void Http_allowed_to_localhost_for_tests() => Assert.Equal(UpdateKind.Optional, UpdatePlanner.Plan(V010, Rel("0.1.0", "0.1.1", url: "http://127.0.0.1:8765/N.exe")).Kind);

    [Fact]
    public async Task Verifies_sha256()
    {
        using var tmp = new TempDir();
        string f = Path.Combine(tmp.Path, "a.bin");
        File.WriteAllBytes(f, [1, 2, 3]);
        string good = Convert.ToHexString(SHA256.HashData([1, 2, 3]));
        Assert.True(await UpdateVerifier.VerifyAsync(f, good));
        Assert.True(await UpdateVerifier.VerifyAsync(f, good.ToLowerInvariant()));
        Assert.False(await UpdateVerifier.VerifyAsync(f, Sha));
        Assert.False(await UpdateVerifier.VerifyAsync(f, "nothex"));
    }

    private sealed class BytesHandler(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
    }

    [Fact]
    public async Task Stage_keeps_only_verified_downloads()
    {
        using var tmp = new TempDir();
        byte[] exe = [77, 90, 1, 2, 3, 4];
        string good = Convert.ToHexString(SHA256.HashData(exe));
        var http = new HttpClient(new BytesHandler(exe));
        var installer = new UpdateInstaller(http, Path.Combine(tmp.Path, "update"));

        var plan = new UpdatePlan(UpdateKind.Optional, V010, new(0, 1, 1), "https://h/Nostalgia.exe", good);
        string staged = await installer.StageAsync(plan, "Nostalgia.exe", null, CancellationToken.None);
        Assert.Equal(exe, File.ReadAllBytes(staged));

        var bad = plan with { Sha256 = Sha, Target = new(0, 1, 2) };
        await Assert.ThrowsAsync<UpdateVerificationException>(() => installer.StageAsync(bad, "Nostalgia.exe", null, CancellationToken.None));
        Assert.Single(Directory.GetFiles(installer.UpdateDir)); // only the first, verified file
    }

    [Fact]
    public async Task Apply_replaces_the_target_once_the_old_process_is_gone()
    {
        using var tmp = new TempDir();
        string staged = Path.Combine(tmp.Path, "new.exe"), target = Path.Combine(tmp.Path, "Nostalgia.exe");
        File.WriteAllText(staged, "new");
        File.WriteAllText(target, "old");
        await UpdateInstaller.ApplyAsync(staged, target, int.MaxValue /* no such process */, TimeSpan.FromSeconds(1));
        Assert.Equal("new", File.ReadAllText(target));
    }
}

public class PasteTests
{
    [Theory]
    [InlineData("  Abc123  ", "Abc123")]
    [InlineData("Abc123\r\n", "Abc123")]
    [InlineData("\tAbc 123\n", "Abc 123")]
    [InlineData("\u00A0Abc\u200B", "Abc")]
    [InlineData("\uFEFFAbc", "Abc")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Trims_pasted_values(string? input, string expected) => Assert.Equal(expected, PasteSanitizer.Clean(input));

    [Theory]
    [InlineData("Bob1", true)]
    [InlineData("bob_1", false)]
    [InlineData("", false)]
    [InlineData("Bøb", false)]
    public void Account_name_rule(string name, bool ok) => Assert.Equal(ok, LoginCredential.IsValidAccountName(name));

    [Fact]
    public void Credential_ToString_hides_the_password() =>
        Assert.DoesNotContain("s3cret", new LoginCredential("Bob", "s3cret").ToString());
}

public class ClientTests
{
    /// <summary>Minimal "PE" with a VS_FIXEDFILEINFO block: enough for the version reader.</summary>
    internal static byte[] FakePe(int a, int b, int c, int d)
    {
        var data = new byte[256];
        data[0] = (byte)'M'; data[1] = (byte)'Z';
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(100), 0xFEEF04BD);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(108), (uint)(a << 16 | b));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(112), (uint)(c << 16 | d));
        return data;
    }

    [Fact]
    public void Reads_daoc_version_numbers()
    {
        var v = PeVersionReader.ReadFileVersion(FakePe(1, 1, 2, 7));
        Assert.Equal(new Version(1, 1, 2, 7), v);
        Assert.Equal(1127, PeVersionReader.ToClientNumber(v!));
        Assert.Equal("1.127", PeVersionReader.ToClientString(1127));
        Assert.Null(PeVersionReader.ReadFileVersion(new byte[100]));
    }

    [Fact]
    public void Validates_client_folders()
    {
        using var tmp = new TempDir();
        var info = new ClientInfo();
        Assert.Equal(ClientStatus.FolderMissing, ClientValidator.Check(Path.Combine(tmp.Path, "nope"), info).Status);
        Assert.Equal(ClientStatus.NoGameDll, ClientValidator.Check(tmp.Path, info).Status);

        File.WriteAllBytes(Path.Combine(tmp.Path, "game.dll"), FakePe(1, 1, 3, 1)); // patched to live (1.131)
        var live = ClientValidator.Check(tmp.Path, info);
        Assert.Equal(ClientStatus.UnsupportedVersion, live.Status);
        Assert.Equal("1.131", live.VersionText);

        File.WriteAllBytes(Path.Combine(tmp.Path, "game1127.dll"), FakePe(1, 1, 2, 7));
        var noConnect = ClientValidator.Check(tmp.Path, info);
        Assert.Equal(ClientStatus.NoConnectTool, noConnect.Status);
        Assert.Equal("game1127.dll", noConnect.GameDll);

        File.WriteAllBytes(Path.Combine(tmp.Path, "connect.exe"), [0]);
        var ok = ClientValidator.Check(tmp.Path, info);
        Assert.True(ok.Ok);
        Assert.Equal("game1127.dll", ok.GameDll);
        Assert.Equal(1127, ok.Version);
    }

    [Fact]
    public void Find_first_prefers_a_valid_folder_and_otherwise_the_most_specific_problem()
    {
        using var a = new TempDir();
        using var b = new TempDir();
        File.WriteAllBytes(Path.Combine(b.Path, "game.dll"), FakePe(1, 1, 2, 7));
        var best = ClientValidator.FindFirst(["", "Z:\\does-not-exist", a.Path, b.Path], new ClientInfo());
        Assert.Equal(ClientStatus.NoConnectTool, best.Status);
        Assert.Equal(b.Path, best.Folder);

        File.WriteAllBytes(Path.Combine(b.Path, "connect.exe"), [0]);
        Assert.True(ClientValidator.FindFirst([a.Path, b.Path], new ClientInfo()).Ok);
    }

    [Fact]
    public void Real_client_if_present()
    {
        const string folder = @"D:\daocclient";
        if (!OperatingSystem.IsWindows() || !Directory.Exists(folder)) return; // owner's PC only
        var c = ClientValidator.Check(folder, new ClientInfo());
        Assert.True(c.Ok, c.Status.ToString());
        Assert.Equal(1127, c.Version);
    }

    [Theory]
    [InlineData(false, null, LaunchOutcome.NeverStarted)]
    [InlineData(true, 5, LaunchOutcome.EarlyExit)]
    [InlineData(true, 60, LaunchOutcome.EarlyExit)]
    [InlineData(true, 600, LaunchOutcome.Played)]
    public void Launch_assessment(bool started, int? seconds, LaunchOutcome expected) =>
        Assert.Equal(expected, LaunchAssessment.Assess(started, seconds is null ? null : TimeSpan.FromSeconds(seconds.Value)));
}

public class MiscTests
{
    [Fact]
    public void Quickbar_step_needs_flag_url_and_module()
    {
        var none = new QuickbarServiceUnavailable();
        var real = new QuickbarService(new FakeQuickbarApi(), new FakeIniLocator(null), new FakeProcesses());
        Assert.False(QuickbarFeature.Active(true, "https://x", none));
        Assert.False(QuickbarFeature.Active(false, "https://x", real));
        Assert.False(QuickbarFeature.Active(true, null, real));
        Assert.True(QuickbarFeature.Active(true, "https://x", real));
    }

    [Fact]
    public void Support_info_has_phase_versions_and_no_password_field()
    {
        string text = SupportInfo.Build(new SupportInfoData
        {
            LauncherVersion = "0.1.0", ArtVersion = "57cfded74e63", Platform = "windows", OsDescription = "Windows",
            PhaseId = "beta", PhaseNoticeVersion = 1, ManifestSource = "Remote", ServerHost = "h", ServerPort = 10300,
            AccountName = "Bob", CredentialStore = "DPAPI", DataFolder = "d", LogFolder = "l", RecentErrors = ["e1"],
        }, DateTimeOffset.Now);
        Assert.Contains("Launcher: 0.1.0", text);
        Assert.Contains("Art: 57cfded74e63", text);
        Assert.Contains("Phase: beta (notice v1)", text);
        Assert.Contains("Account: Bob", text);
        Assert.Contains("e1", text);
        Assert.DoesNotContain(typeof(SupportInfoData).GetProperties(), p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Status_probe_reports_online_and_offline()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var on = await Nostalgia.Launcher.Core.Status.ServerStatusProbe.ProbeAsync("127.0.0.1", port, TimeSpan.FromSeconds(2));
        listener.Stop();
        Assert.True(on.Online);
        Assert.NotNull(on.LatencyMs);
        var off = await Nostalgia.Launcher.Core.Status.ServerStatusProbe.ProbeAsync("127.0.0.1", port, TimeSpan.FromSeconds(2));
        Assert.False(off.Online);
    }
}
