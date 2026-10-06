using Nostalgia.Launcher.Core;
using Nostalgia.Launcher.Core.Manifest;
using Xunit;

namespace Nostalgia.Launcher.Tests;

public class ManifestTests
{
    internal const string Full = """
    // comment allowed
    {
      "schemaVersion": 1,
      "server": { "name": "Nostalgia", "host": "play.example.org", "loginPort": 10300, "regionPort": 10400, "quickbarUrl": "https://play.example.org" },
      "client": { "supportedVersions": [1127], "dllNames": ["game1127.dll", "game.dll"], "infoUrl": "https://www.opendaoc.com/docs/client/" },
      "launcher": { "minimumVersion": "0.1.0", "latestVersion": "0.1.1",
                    "downloadUrl": "https://github.com/x/y/releases/download/v0.1.1/Nostalgia.exe",
                    "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef" },
      "links": { "website": "https://example.org", "discordInvite": "https://discord.gg/abc", "feedbackChannel": "https://discord.com/channels/1/2" },
      "bot": { "registerCommand": "/register", "resetCommand": "/reset" },
      "features": { "quickbars": true },
      "phase": { "id": "beta", "badge": "BETA", "title": "Nostalgia PvP-Freeshard – Beta-Test", "noticeDe": "DE", "noticeEn": "EN", "noticeVersion": 3 },
      "news": [ { "title": "A", "date": "2026-10-05", "text": "t", "link": "https://example.org/a" } ],
    }
    """;

    [Fact]
    public void Parses_full_manifest_with_comments_and_trailing_commas()
    {
        var r = ManifestParser.Parse(Full);
        Assert.True(r.Ok, r.Error);
        var m = r.Manifest!;
        Assert.Equal("play.example.org", m.Server.Host);
        Assert.Equal(10300, m.Server.LoginPort);
        Assert.Equal("https://play.example.org", m.Server.QuickbarUrl);
        Assert.Equal([1127], m.Client.SupportedVersions);
        Assert.Equal("0.1.1", m.Launcher.LatestVersion);
        Assert.Equal("https://discord.com/channels/1/2", m.Links.FeedbackChannel);
        Assert.True(m.Features.Quickbars);
        Assert.NotNull(m.Phase);
        Assert.Equal("BETA", m.Phase!.Badge);
        Assert.Equal(3, m.Phase.NoticeVersion);
        Assert.Single(m.News);
        Assert.Equal(new DateOnly(2026, 10, 5), m.News[0].ParsedDate);
    }

    [Fact]
    public void Phase_null_means_no_phase()
    {
        var r = ManifestParser.Parse(Full.Replace("""
            "phase": { "id": "beta", "badge": "BETA", "title": "Nostalgia PvP-Freeshard – Beta-Test", "noticeDe": "DE", "noticeEn": "EN", "noticeVersion": 3 },
            """, "\"phase\": null,"));
        Assert.True(r.Ok, r.Error);
        Assert.Null(r.Manifest!.Phase);
    }

    [Fact]
    public void Missing_phase_means_no_phase()
    {
        var r = ManifestParser.Parse("""{ "schemaVersion": 1, "server": { "host": "h" }, "launcher": { "minimumVersion": "0.0.0", "latestVersion": "0.1.0" } }""");
        Assert.True(r.Ok, r.Error);
        Assert.Null(r.Manifest!.Phase);
        Assert.Equal(10300, r.Manifest.Server.LoginPort);
        Assert.Equal("/register", r.Manifest.Bot.RegisterCommand);
        Assert.False(r.Manifest.Features.Quickbars);
    }

    [Fact]
    public void Unknown_fields_and_newer_schema_are_tolerated()
    {
        var r = ManifestParser.Parse("""
        { "schemaVersion": 2, "somethingNew": { "x": 1 },
          "server": { "host": "h", "extra": true },
          "launcher": { "minimumVersion": "0.0.0", "latestVersion": "0.1.0", "channel": "beta" },
          "phase": { "id": "beta", "badge": "BETA", "noticeVersion": 1, "color": "#fff" } }
        """);
        Assert.True(r.Ok, r.Error);
        Assert.Equal("beta", r.Manifest!.Phase!.Id);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 1, "server": { "host": "" }, "launcher": { "minimumVersion": "0.0.0", "latestVersion": "0.1.0" } }""", "server.host")]
    [InlineData("""{ "server": { "host": "h" }, "launcher": { "minimumVersion": "0.0.0", "latestVersion": "0.1.0" } }""", "schemaVersion")]
    [InlineData("""{ "schemaVersion": 1, "server": { "host": "h" }, "launcher": { "minimumVersion": "x", "latestVersion": "0.1.0" } }""", "minimumVersion")]
    [InlineData("""{ "schemaVersion": 1, "server": { "host": "h" }, "launcher": { "minimumVersion": "0.0.0", "latestVersion": "0.1.0", "sha256": "abc" } }""", "sha256")]
    [InlineData("""{ "schemaVersion": 1, "server": { "host": "h" }, "launcher": { "minimumVersion": "0.0.0", "latestVersion": "0.1.0" }, "phase": { "badge": "BETA" } }""", "phase.id")]
    [InlineData("""{ "schemaVersion": 1, "server": { "host": "h", "loginPort": 70000 }, "launcher": { "minimumVersion": "0.0.0", "latestVersion": "0.1.0" } }""", "loginPort")]
    [InlineData("""{ "schemaVersion": 1, "server": { "host": "h", "quickbarUrl": "http://play.example.org" }, "launcher": { "minimumVersion": "0.0.0", "latestVersion": "0.1.0" } }""", "quickbarUrl")]
    [InlineData("""{ "schemaVersion": 1, "server": { "host": "h", "quickbarUrl": "ftp://x" }, "launcher": { "minimumVersion": "0.0.0", "latestVersion": "0.1.0" } }""", "quickbarUrl")]
    [InlineData("""{ not json""", "JSON")]
    public void Invalid_manifests_are_rejected(string json, string expected)
    {
        var r = ManifestParser.Parse(json);
        Assert.False(r.Ok);
        Assert.Contains(expected, r.Error);
    }

    [Fact]
    public void Repo_manifests_are_valid()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "manifests");
        var files = Directory.GetFiles(dir, "*.json");
        Assert.NotEmpty(files);
        foreach (var f in files)
        {
            var r = ManifestParser.Parse(File.ReadAllText(f));
            Assert.True(r.Ok, $"{Path.GetFileName(f)}: {r.Error}");
        }
    }

    [Fact]
    public void Production_manifest_carries_the_fixed_beta_text()
    {
        var m = ManifestParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "manifests", "production-launcher.json"))).Manifest!;
        Assert.Equal(BuiltInDefaults.BetaNoticeDe, m.Phase!.NoticeDe);
        Assert.Equal(BuiltInDefaults.BetaNoticeEn, m.Phase.NoticeEn);
        Assert.Equal("beta", m.Phase.Id);
    }

    // ── ManifestService ───────────────────────────────────────────────────────────────────

    private sealed class FakeFetcher(Func<string> fetch) : IManifestFetcher
    {
        public Task<string> FetchAsync(string location, CancellationToken ct) => Task.FromResult(fetch());
    }

    [Fact]
    public async Task Remote_success_is_cached_and_used_when_remote_fails_later()
    {
        using var tmp = new TempDir();
        string cache = Path.Combine(tmp.Path, "cache.json");
        var v = new LauncherVersion(0, 1, 0);

        var ok = await new ManifestService(new FakeFetcher(() => Full), cache, v).LoadAsync("x");
        Assert.Equal(ManifestSource.Remote, ok.Source);
        Assert.True(File.Exists(cache));

        var offline = await new ManifestService(new FakeFetcher(() => throw new HttpRequestException("offline")), cache, v).LoadAsync("x");
        Assert.Equal(ManifestSource.Cache, offline.Source);
        Assert.Equal("play.example.org", offline.Manifest.Server.Host);
        Assert.Contains("offline", offline.RemoteError);

        var invalid = await new ManifestService(new FakeFetcher(() => "{}"), cache, v).LoadAsync("x");
        Assert.Equal(ManifestSource.Cache, invalid.Source);
        Assert.Contains("invalid manifest", invalid.RemoteError);
    }

    [Fact]
    public async Task Never_loaded_falls_back_to_built_in_beta_for_0x_builds_only()
    {
        using var tmp = new TempDir();
        var fail = new FakeFetcher(() => throw new HttpRequestException("offline"));

        var beta = await new ManifestService(fail, Path.Combine(tmp.Path, "a.json"), new LauncherVersion(0, 1, 0)).LoadAsync("x");
        Assert.Equal(ManifestSource.BuiltIn, beta.Source);
        Assert.Equal(BuiltInDefaults.BetaNoticeDe, beta.Manifest.Phase!.NoticeDe);
        Assert.Equal("", beta.Manifest.Server.Host);

        var live = await new ManifestService(fail, Path.Combine(tmp.Path, "b.json"), new LauncherVersion(1, 0, 0)).LoadAsync("x");
        Assert.Equal(ManifestSource.BuiltIn, live.Source);
        Assert.Null(live.Manifest.Phase);
    }

    [Fact]
    public async Task Http_fetcher_reads_local_files()
    {
        using var tmp = new TempDir();
        string f = Path.Combine(tmp.Path, "m.json");
        File.WriteAllText(f, Full);
        var r = await new ManifestService(new HttpManifestFetcher(new HttpClient()), Path.Combine(tmp.Path, "c.json"), new LauncherVersion(0, 1, 0)).LoadAsync(f);
        Assert.Equal(ManifestSource.Remote, r.Source);
    }
}

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("nostalgia-test-").FullName;
    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
