using System.Net;
using System.Net.Sockets;
using System.Text;
using Nostalgia.Launcher.Core.Accounts;
using Nostalgia.Launcher.Core.Platform;
using Nostalgia.Launcher.Core.Quickbars;
using Nostalgia.Launcher.ViewModels;
using Xunit;

namespace Nostalgia.Launcher.Tests;

internal sealed class FakeQuickbarApi : IQuickbarApi
{
    public PendingResponse Pending { get; set; } = new() { FormatVersion = 1 };
    public QuickbarApiException? PendingError { get; set; }
    public QuickbarApiException? AckError { get; set; }
    public List<IReadOnlyList<string>> Acks { get; } = [];
    public int PendingCalls { get; private set; }

    public Task<PendingResponse> PendingAsync(string baseUrl, LoginCredential login, string launcherVersion, CancellationToken ct)
    {
        PendingCalls++;
        return PendingError is { } e ? Task.FromException<PendingResponse>(e) : Task.FromResult(Pending);
    }

    public Task<int> AckAsync(string baseUrl, LoginCredential login, IReadOnlyList<string> pendingIds, CancellationToken ct)
    {
        Acks.Add(pendingIds);
        return AckError is { } e ? Task.FromException<int>(e) : Task.FromResult(pendingIds.Count);
    }
}

internal sealed class FakeIniLocator(string? folder) : IQuickbarIniLocator
{
    public string? SettingsFolder(string clientFolder) => folder;
}

internal sealed class FakeProcesses : IProcessWatcher
{
    public bool Running { get; set; }
    public IReadOnlyList<int> FindGameProcesses(string clientFolder, string gameDll) => [];
    public Task<int?> WaitForGameStartAsync(string f, string d, IReadOnlySet<int> ignore, TimeSpan timeout, CancellationToken ct) => Task.FromResult<int?>(null);
    public Task WaitForExitAsync(int pid, CancellationToken ct) => Task.CompletedTask;
    public bool AnyGameRunning(IReadOnlyList<string> dllNames) => Running;
}

internal static class QuickbarSamples
{
    /// <summary>
    /// Trimmed copy of a calibration INI (staging, 2026-10-06; character name removed): bars with spells, an ability, an item (type 9)
    /// and a macro (type 52), bank 10 (Hotkey_99), Panels with QB3 hidden, sections with blank lines after them.
    /// </summary>
    public const string Calibration = """
        [Panels]
        Version=1
        Alpha=100
        Help=1,1439,348,590,471,100,100
        LagMeter=1479,0,0,100,0,100
        Quickbar=1248,712,1,1,100,9,1
        Quickbar2=1673,715,1,1,100,0,1
        Quickbar3=2008,893,0,1,100,0,0
        SpellEffect=10,4,0
        HookpointStore=100,100,100,100,
        Emoticon=100
        [Quickbar]
        GroupSize=10
        Hotkey_0=8,3,Quickcast,400
        Hotkey_1=10,19,Diamond Shield (Enhanced),1009
        Hotkey_2=10,424,Major Conflagration,1344
        Hotkey_3=10,13,Buffer of Earth,2332
        Hotkey_5=9,40,Personal Bind Recall Stone---,156
        Hotkey_6=52,0,Macro #0,156
        Hotkey_9=10,120,Fiery Maelstrom (Major),1069
        Hotkey_10=10,110,Fire Storm,1067
        Hotkey_19=10,120,Fiery Maelstrom (Major),1069
        Hotkey_99=10,210,Fingers Of Ice (Lesser),1047
        [Quickbar2]
        GroupSize=10
        Hotkey_0=10,17,Buffer of Stone,2334
        Hotkey_2=10,101,Summon Fire,1065
        [Quickbar3]
        GroupSize=10
        Hotkey_0=10,108,Fire Storm (Minor),1066
        [Macros]
        Macro_0=test,/say hi
        [Chat]
        ChannelVer=1
        IgnoreChatScrolling0=1
        [NameOptions]
        Version=3
        Visibility=126

        [QuickBinds]
        Hotkey_0=77,5,Something Unknown,12

        [Camera]
        distance=500.00

        """;

    public static string Crlf(string s) => s.ReplaceLineEndings("\r\n");

    public static Dictionary<int, IReadOnlyList<IniSlot>> Layout(params (int Bar, int Hotkey, int Type, int Value, string Name)[] slots)
    {
        var d = new Dictionary<int, IReadOnlyList<IniSlot>> { [1] = [], [2] = [], [3] = [] };
        foreach (var g in slots.GroupBy(s => s.Bar))
            d[g.Key] = g.Select(s => new IniSlot(s.Hotkey, s.Type, s.Value, s.Name)).ToList();
        return d;
    }

    /// <summary>Lines of a section (without the header), in file order.</summary>
    public static List<string> Section(string ini, string name)
    {
        var result = new List<string>();
        bool inside = false;
        foreach (string line in ini.ReplaceLineEndings("\n").Split('\n'))
        {
            string t = line.Trim();
            if (t.StartsWith('[') && t.EndsWith(']')) { inside = t == $"[{name}]"; continue; }
            if (inside) result.Add(line);
        }
        return result;
    }

    /// <summary>The file with the three bar sections' bodies and the two Panels lines blanked out, for "everything else unchanged".</summary>
    public static string WithoutBars(string ini)
    {
        var sb = new StringBuilder();
        string? section = null;
        foreach (string line in ini.Split('\n'))
        {
            string t = line.Trim('\r').Trim();
            if (t.StartsWith('[') && t.EndsWith(']')) section = t;
            bool bar = section is "[Quickbar]" or "[Quickbar2]" or "[Quickbar3]" && !t.StartsWith('[') && t.Length > 0;
            bool panel = section == "[Panels]" && (t.StartsWith("Quickbar2=") || t.StartsWith("Quickbar3="));
            if (!bar && !panel) sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    public static PendingCharacter Character(string name, string id, params (int Bar, int Bank, int Slot, int Type, int Value, string Name)[] slots) => new()
    {
        PendingId = id,
        Name = name,
        Class = "Wizard",
        Preset = "Fire",
        Bars = slots.GroupBy(s => s.Bar).ToDictionary(g => g.Key.ToString(), g => g.Select(s => new PendingSlot
        {
            Bank = s.Bank, Slot = s.Slot, Hotkey = (s.Bank - 1) * 10 + (s.Slot - 1), Type = s.Type, Value = s.Value, Name = s.Name,
        }).ToList()),
    };
}

public class QuickbarIniWriterTests
{
    private static readonly Dictionary<int, IReadOnlyList<IniSlot>> Wizard = QuickbarSamples.Layout(
        (1, 0, 10, 424, "Major Conflagration"), (1, 1, 10, 120, "Fiery Maelstrom (Major)"), (1, 9, 8, 3, "Quickcast"),
        (2, 0, 10, 17, "Buffer of Stone"),
        (3, 0, 10, 108, "Fire Storm (Minor)"));

    [Fact]
    public void Empty_file_gets_three_sections()
    {
        string result = QuickbarIniWriter.Apply("", Wizard);
        Assert.Equal(QuickbarSamples.Crlf("""
            [Quickbar]
            GroupSize=10
            Hotkey_0=10,424,Major Conflagration,0
            Hotkey_1=10,120,Fiery Maelstrom (Major),0
            Hotkey_9=8,3,Quickcast,0
            [Quickbar2]
            GroupSize=10
            Hotkey_0=10,17,Buffer of Stone,0
            [Quickbar3]
            GroupSize=10
            Hotkey_0=10,108,Fire Storm (Minor),0

            """), result);
    }

    [Fact]
    public void Full_bars_are_replaced_completely_and_everything_else_stays()
    {
        string ini = QuickbarSamples.Crlf(QuickbarSamples.Calibration);
        string result = QuickbarIniWriter.Apply(ini, Wizard);

        Assert.Equal(["GroupSize=10", "Hotkey_0=10,424,Major Conflagration,0", "Hotkey_1=10,120,Fiery Maelstrom (Major),0", "Hotkey_9=8,3,Quickcast,0"],
            QuickbarSamples.Section(result, "Quickbar"));
        Assert.Equal(["GroupSize=10", "Hotkey_0=10,17,Buffer of Stone,0"], QuickbarSamples.Section(result, "Quickbar2"));
        Assert.Equal(["GroupSize=10", "Hotkey_0=10,108,Fire Storm (Minor),0"], QuickbarSamples.Section(result, "Quickbar3"));
        // The item (type 9), the macro slot (type 52) and bank 10 are gone from bar 1.
        Assert.DoesNotContain("Personal Bind Recall Stone", result);
        Assert.DoesNotContain("Macro #0", result);
        Assert.DoesNotContain("Hotkey_99", result);

        Assert.Equal(QuickbarSamples.WithoutBars(ini), QuickbarSamples.WithoutBars(result));
        Assert.DoesNotContain("\n", result.Replace("\r\n", ""));
    }

    [Fact]
    public void Other_sections_and_unknown_entry_types_outside_the_bars_are_untouched()
    {
        string ini = QuickbarSamples.Crlf(QuickbarSamples.Calibration + "[Quickbar4]\nHotkey_0=99,1,Whatever,3\n");
        string result = QuickbarIniWriter.Apply(ini, Wizard);
        Assert.Equal(["Hotkey_0=77,5,Something Unknown,12", ""], QuickbarSamples.Section(result, "QuickBinds"));
        Assert.Equal(["Macro_0=test,/say hi"], QuickbarSamples.Section(result, "Macros"));
        Assert.Equal(["Hotkey_0=99,1,Whatever,3", ""], QuickbarSamples.Section(result, "Quickbar4"));
        Assert.Equal(QuickbarSamples.Section(ini, "Camera"), QuickbarSamples.Section(result, "Camera"));
        // Line order of the whole file outside the bars: identical.
        Assert.Equal(QuickbarSamples.WithoutBars(ini), QuickbarSamples.WithoutBars(result));
    }

    [Fact]
    public void All_three_bars_are_cleared_when_the_layout_has_no_slots_for_them()
    {
        string ini = QuickbarSamples.Crlf(QuickbarSamples.Calibration);
        string result = QuickbarIniWriter.Apply(ini, QuickbarSamples.Layout());
        foreach (string s in new[] { "Quickbar", "Quickbar2", "Quickbar3" })
            Assert.Equal(["GroupSize=10"], QuickbarSamples.Section(result, s));
        // Nothing to show → the Panels lines stay as they were (QB3 stays hidden).
        Assert.Contains("Quickbar3=2008,893,0,1,100,0,0\r\n", result);
    }

    [Fact]
    public void Qb3_bank_10_is_written_and_qb2_qb3_are_made_visible()
    {
        string ini = QuickbarSamples.Crlf(QuickbarSamples.Calibration);
        var layout = QuickbarSamples.Layout((2, 5, 35, 107, "Slam"), (3, 99, 10, 210, "Fingers Of Ice (Lesser)"));
        string result = QuickbarIniWriter.Apply(ini, layout);
        Assert.Equal(["GroupSize=10", "Hotkey_99=10,210,Fingers Of Ice (Lesser),0"], QuickbarSamples.Section(result, "Quickbar3"));
        Assert.Equal(["GroupSize=10", "Hotkey_5=35,107,Slam,0"], QuickbarSamples.Section(result, "Quickbar2"));
        Assert.Contains("Quickbar2=1673,715,1,1,100,0,1\r\n", result);
        Assert.Contains("Quickbar3=2008,893,1,1,100,0,1\r\n", result); // fields 3 and 7 from 0 to 1, bank field unchanged
        Assert.Contains("Quickbar=1248,712,1,1,100,9,1\r\n", result);  // bar 1 line never touched
        Assert.Equal(["GroupSize=10"], QuickbarSamples.Section(result, "Quickbar"));
    }

    [Fact]
    public void Missing_bar_sections_are_created_next_to_the_existing_ones()
    {
        string ini = QuickbarSamples.Crlf("[Panels]\nVersion=1\n[Quickbar]\nGroupSize=10\n\n[Macros]\nMacro_0=a,/say b\n");
        string result = QuickbarIniWriter.Apply(ini, QuickbarSamples.Layout((1, 0, 8, 3, "Quickcast"), (3, 99, 10, 210, "Fingers Of Ice (Lesser)")));
        Assert.Equal(QuickbarSamples.Crlf("""
            [Panels]
            Version=1
            [Quickbar]
            GroupSize=10
            Hotkey_0=8,3,Quickcast,0
            [Quickbar3]
            GroupSize=10
            Hotkey_99=10,210,Fingers Of Ice (Lesser),0

            [Macros]
            Macro_0=a,/say b

            """), result);
    }

    [Fact]
    public void Crlf_lf_bom_latin1_bytes_and_a_missing_final_line_break_are_kept()
    {
        // LF file stays LF.
        string lf = QuickbarSamples.Calibration;
        string lfResult = QuickbarIniWriter.Apply(lf, Wizard);
        Assert.DoesNotContain("\r", lfResult);

        // Bytes: UTF-8 BOM, a Latin-1 byte (0xE9) in another section, no line break at the end of the file.
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "Test-5.ini");
        var bytes = new List<byte>([0xEF, 0xBB, 0xBF]);
        bytes.AddRange(Encoding.Latin1.GetBytes("[Panels]\r\nVersion=1\r\n[Quickbar]\r\nGroupSize=10\r\nHotkey_0=8,3,Quickcast,400\r\n[Chat]\r\nName=Caf\u00E9"));
        File.WriteAllBytes(path, bytes.ToArray());

        QuickbarIniWriter.ApplyToFile(path, QuickbarSamples.Layout((1, 1, 10, 19, "Diamond Shield (Enhanced)")));

        byte[] after = File.ReadAllBytes(path);
        var expected = new List<byte>([0xEF, 0xBB, 0xBF]);
        expected.AddRange(Encoding.Latin1.GetBytes("[Panels]\r\nVersion=1\r\n[Quickbar]\r\nGroupSize=10\r\nHotkey_1=10,19,Diamond Shield (Enhanced),0\r\n[Chat]\r\nName=Caf\u00E9"));
        Assert.Equal(expected.ToArray(), after);
        Assert.False(File.Exists(path + ".nostalgia.tmp"));
    }

    [Fact]
    public void Last_section_without_final_line_break_keeps_that_shape()
    {
        string result = QuickbarIniWriter.Apply("[Quickbar]\r\nGroupSize=10", QuickbarSamples.Layout((1, 0, 8, 3, "Quickcast")));
        Assert.Equal("[Quickbar]\r\nGroupSize=10\r\nHotkey_0=8,3,Quickcast,0", result);
    }

    [Fact]
    public void Writing_twice_gives_the_same_file()
    {
        string once = QuickbarIniWriter.Apply(QuickbarSamples.Crlf(QuickbarSamples.Calibration), Wizard);
        Assert.Equal(once, QuickbarIniWriter.Apply(once, Wizard));
    }

    [Theory]
    [InlineData("Guard, III")]
    [InlineData("Line\nBreak")]
    [InlineData("")]
    [InlineData("Ω Omega")]
    public void Names_the_client_cannot_read_back_are_refused(string name)
    {
        Assert.False(QuickbarIniWriter.IsWritableName(name));
        Assert.Throws<ArgumentException>(() => QuickbarIniWriter.Apply("", QuickbarSamples.Layout((1, 0, 10, 1, name))));
    }
}

public class QuickbarIniPathTests
{
    [Theory]
    [InlineData("[paths]\r\nsettings=Atlas1", "Atlas")]          // installer file: no final line break → last char dropped
    [InlineData("[paths]\r\nsettings=Atlas1\r\n", "Atlas1")]
    [InlineData("[paths]\nsettings=Atlas1\n", "Atlas1")]
    [InlineData("[Paths]\r\nSettings = Atlas1\r\n", "Atlas1")]
    [InlineData("[other]\r\nsettings=Atlas1\r\n", null)]
    [InlineData("[paths]\r\nsettings=\r\n", null)]
    [InlineData("[paths]\r\nsettings=..\r\n", null)]
    [InlineData("", null)]
    public void Settings_folder_name_follows_the_client_reader(string pathsDat, string? expected)
    {
        Assert.Equal(expected, QuickbarIniPaths.SettingsFolderName(Encoding.Latin1.GetBytes(pathsDat)));
    }

    [Fact]
    public void Ini_file_name_and_folder()
    {
        Assert.Equal("Asdasd-5.ini", QuickbarIniPaths.FileName("Asdasd"));
        using var client = new TempDir();
        Assert.Null(QuickbarIniPaths.SettingsFolder(@"C:\root", client.Path));
        File.WriteAllText(Path.Combine(client.Path, "paths.dat"), "[paths]\r\nsettings=Atlas1");
        Assert.Equal(Path.Combine(@"C:\root", "Atlas"), QuickbarIniPaths.SettingsFolder(@"C:\root", client.Path));
    }
}

public class QuickbarApiTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(Uri Uri, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            // The server accepts Content-Length bodies only (chunked → 501).
            Assert.NotEqual(true, request.Headers.TransferEncodingChunked);
            Assert.NotNull(request.Content?.Headers.ContentLength);
            Assert.Equal("application/json", request.Content!.Headers.ContentType?.MediaType);
            Requests.Add((request.RequestUri!, await request.Content.ReadAsStringAsync(ct)));
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json) =>
        new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static readonly LoginCredential Login = new("Bob1", "pw");

    [Fact]
    public async Task Pending_and_ack_use_the_server_format()
    {
        var h = new Handler(r => r.RequestUri!.AbsolutePath.EndsWith("/pending")
            ? Json(HttpStatusCode.OK, """
              {"formatVersion":1,"characters":[{"pendingId":"0123456789abcdef0123456789abcdef","name":"Asdasd","realm":"Albion",
               "class":"Scout","preset":"Archer","weapon":null,"createdUtc":"2026-10-06T12:00:00Z",
               "bars":{"1":[{"bank":1,"slot":1,"hotkey":0,"type":35,"value":8,"name":"Lunge"}],"2":[],"3":[]}}]}
              """)
            : Json(HttpStatusCode.OK, """{"acknowledged":1}"""));
        var api = new QuickbarHttpApi(new HttpClient(h));

        var p = await api.PendingAsync("https://connect.example.org/", Login, "0.2.0", CancellationToken.None);
        var c = Assert.Single(p.Characters);
        Assert.Equal("Asdasd", c.Name);
        Assert.Equal("Lunge", Assert.Single(c.Bars["1"]).Name);
        Assert.Equal(1, await api.AckAsync("https://connect.example.org", Login, [c.PendingId], CancellationToken.None));

        Assert.Equal("https://connect.example.org/quickbar/pending", h.Requests[0].Uri.ToString());
        Assert.Contains("\"account\":\"Bob1\"", h.Requests[0].Body);
        Assert.Contains("\"launcherVersion\":\"0.2.0\"", h.Requests[0].Body);
        Assert.Equal("https://connect.example.org/quickbar/ack", h.Requests[1].Uri.ToString());
        Assert.Contains("\"pendingIds\":[\"0123456789abcdef0123456789abcdef\"]", h.Requests[1].Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":"login failed"}""", QuickbarApiError.LoginFailed)]
    [InlineData(HttpStatusCode.ServiceUnavailable, """{"error":"off"}""", QuickbarApiError.Unavailable)]
    [InlineData(HttpStatusCode.TooManyRequests, """{"error":"slow down"}""", QuickbarApiError.Unavailable)]
    [InlineData(HttpStatusCode.OK, """{"formatVersion":2,"characters":[]}""", QuickbarApiError.Unavailable)]
    [InlineData(HttpStatusCode.OK, """<html>""", QuickbarApiError.Unavailable)]
    public async Task Errors_are_mapped(HttpStatusCode code, string body, QuickbarApiError expected)
    {
        var api = new QuickbarHttpApi(new HttpClient(new Handler(_ => Json(code, body))));
        var e = await Assert.ThrowsAsync<QuickbarApiException>(() => api.PendingAsync("https://x.example.org", Login, "0.2.0", CancellationToken.None));
        Assert.Equal(expected, e.Error);
    }

    [Theory]
    [InlineData("https://connect.nostalgiapvp.com", true)]
    [InlineData("http://127.0.0.1:10380", true)]
    [InlineData("http://localhost:10380", true)]
    [InlineData("http://connect.nostalgiapvp.com", false)]
    [InlineData("https://connect.nostalgiapvp.com/?x=1", false)]
    [InlineData("ftp://127.0.0.1", false)]
    [InlineData("connect.nostalgiapvp.com", false)]
    public void Only_https_or_local_http(string url, bool allowed)
    {
        Assert.Equal(allowed, QuickbarHttpApi.IsAllowedBaseUrl(url));
    }

    [Fact]
    public async Task Offline_server_is_unavailable()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop(); // nothing listens on this port now
        var api = new QuickbarHttpApi(new HttpClient());
        var e = await Assert.ThrowsAsync<QuickbarApiException>(() => api.PendingAsync($"http://127.0.0.1:{port}", Login, "0.2.0", CancellationToken.None));
        Assert.Equal(QuickbarApiError.Unavailable, e.Error);
    }
}

public class QuickbarFlowTests
{
    private const string IdA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string IdB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static QuickbarPlayRequest Request() => new("https://connect.example.org", new LoginCredential("Bob1", "pw"), @"C:\client", ["game1127.dll"], "0.2.0");

    private static (FakeQuickbarApi Api, FakeProcesses Processes, QuickbarService Service) Create(string? folder)
    {
        var api = new FakeQuickbarApi();
        var processes = new FakeProcesses();
        return (api, processes, new QuickbarService(api, new FakeIniLocator(folder), processes));
    }

    [Fact]
    public async Task Offline_server_starts_the_game_with_a_notice()
    {
        var (api, _, svc) = Create(null);
        api.PendingError = new QuickbarApiException(QuickbarApiError.Unavailable, "connection refused");
        var r = await svc.BeforePlayAsync(Request(), CancellationToken.None);
        Assert.Equal(QuickbarOutcome.Unavailable, r.Outcome);
        Assert.True(r.StartGame);
        Assert.Empty(api.Acks);
        Assert.Contains("no answer", MainViewModel.QuickbarNoticeText(r));
    }

    [Fact]
    public async Task Wrong_password_starts_the_game_with_a_notice()
    {
        var (api, _, svc) = Create(null);
        api.PendingError = new QuickbarApiException(QuickbarApiError.LoginFailed, "login failed (401)");
        var r = await svc.BeforePlayAsync(Request(), CancellationToken.None);
        Assert.Equal(QuickbarOutcome.LoginFailed, r.Outcome);
        Assert.True(r.StartGame);
        Assert.Contains("login refused", MainViewModel.QuickbarNoticeText(r));
    }

    [Fact]
    public async Task Nothing_pending_says_nothing()
    {
        var (_, _, svc) = Create(null);
        var r = await svc.BeforePlayAsync(Request(), CancellationToken.None);
        Assert.Equal(QuickbarOutcome.NothingPending, r.Outcome);
        Assert.True(r.StartGame);
        Assert.Equal("", MainViewModel.QuickbarNoticeText(r));
    }

    [Fact]
    public async Task Running_client_refuses_writes_nothing_and_does_not_start()
    {
        using var dir = new TempDir();
        string ini = Path.Combine(dir.Path, "Asdasd-5.ini");
        File.WriteAllText(ini, QuickbarSamples.Crlf(QuickbarSamples.Calibration));
        var (api, processes, svc) = Create(dir.Path);
        processes.Running = true;
        api.Pending = new PendingResponse { FormatVersion = 1, Characters = [QuickbarSamples.Character("Asdasd", IdA, (1, 1, 1, 35, 8, "Lunge"))] };

        var r = await svc.BeforePlayAsync(Request(), CancellationToken.None);

        Assert.Equal(QuickbarOutcome.ClientRunning, r.Outcome);
        Assert.False(r.StartGame);
        Assert.Equal(QuickbarSamples.Crlf(QuickbarSamples.Calibration), File.ReadAllText(ini));
        Assert.Empty(api.Acks);
        Assert.Equal("Close the game first, then press Play again.", MainViewModel.QuickbarNoticeText(r));
    }

    [Fact]
    public async Task Missing_ini_keeps_the_character_pending()
    {
        using var dir = new TempDir();
        var (api, _, svc) = Create(dir.Path);
        api.Pending = new PendingResponse { FormatVersion = 1, Characters = [QuickbarSamples.Character("Newbie", IdA, (1, 1, 1, 35, 8, "Lunge"))] };

        var r = await svc.BeforePlayAsync(Request(), CancellationToken.None);

        Assert.Equal(QuickbarOutcome.Processed, r.Outcome);
        Assert.True(r.StartGame);
        Assert.Equal(["Newbie"], r.IniMissing);
        Assert.Empty(api.Acks);
        Assert.Equal("Log in with Newbie once, log out, then press Play again.", MainViewModel.QuickbarNoticeText(r));
    }

    [Fact]
    public async Task Unknown_settings_folder_counts_as_missing_ini()
    {
        var (api, _, svc) = Create(null);
        api.Pending = new PendingResponse { FormatVersion = 1, Characters = [QuickbarSamples.Character("Asdasd", IdA, (1, 1, 1, 35, 8, "Lunge"))] };
        var r = await svc.BeforePlayAsync(Request(), CancellationToken.None);
        Assert.Equal(["Asdasd"], r.IniMissing);
        Assert.Empty(api.Acks);
    }

    [Fact]
    public async Task Partial_success_acks_only_the_written_ids()
    {
        using var dir = new TempDir();
        string ini = Path.Combine(dir.Path, "Asdasd-5.ini");
        File.WriteAllText(ini, QuickbarSamples.Crlf(QuickbarSamples.Calibration));
        var (api, _, svc) = Create(dir.Path);
        api.Pending = new PendingResponse
        {
            FormatVersion = 1,
            Characters =
            [
                QuickbarSamples.Character("Asdasd", IdA, (1, 1, 1, 35, 8, "Lunge"), (2, 1, 1, 8, 10, "Guard III"), (3, 10, 10, 10, 210, "Fingers Of Ice (Lesser)")),
                QuickbarSamples.Character("Newbie", IdB, (1, 1, 1, 35, 8, "Lunge")),
            ],
        };

        var r = await svc.BeforePlayAsync(Request(), CancellationToken.None);

        Assert.Equal(["Asdasd"], r.Written);
        Assert.Equal(["Newbie"], r.IniMissing);
        Assert.Equal([IdA], Assert.Single(api.Acks));
        string text = File.ReadAllText(ini);
        Assert.Equal(["GroupSize=10", "Hotkey_0=35,8,Lunge,0"], QuickbarSamples.Section(text, "Quickbar"));
        Assert.Equal(["GroupSize=10", "Hotkey_0=8,10,Guard III,0"], QuickbarSamples.Section(text, "Quickbar2"));
        Assert.Equal(["GroupSize=10", "Hotkey_99=10,210,Fingers Of Ice (Lesser),0"], QuickbarSamples.Section(text, "Quickbar3"));
        Assert.Equal("Quickbars set up for Asdasd. Log in with Newbie once, log out, then press Play again.", MainViewModel.QuickbarNoticeText(r));
    }

    [Fact]
    public async Task Locked_ini_is_not_acked_and_a_failed_ack_is_reported()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "Locked-5.ini"), "[Quickbar]\r\nGroupSize=10\r\n");
        File.WriteAllText(Path.Combine(dir.Path, "Fine-5.ini"), "[Quickbar]\r\nGroupSize=10\r\n");
        var (api, _, svc) = Create(dir.Path);
        api.AckError = new QuickbarApiException(QuickbarApiError.Unavailable, "timeout");
        api.Pending = new PendingResponse
        {
            FormatVersion = 1,
            Characters = [QuickbarSamples.Character("Locked", IdA, (1, 1, 1, 35, 8, "Lunge")), QuickbarSamples.Character("Fine", IdB, (1, 1, 1, 35, 8, "Lunge"))],
        };

        QuickbarPlayResult r;
        using (new FileStream(Path.Combine(dir.Path, "Locked-5.ini"), FileMode.Open, FileAccess.Read, FileShare.None))
            r = await svc.BeforePlayAsync(Request(), CancellationToken.None);

        Assert.Equal(["Fine"], r.Written);
        Assert.Equal(["Locked"], r.WriteFailed);
        Assert.Equal([IdB], Assert.Single(api.Acks));
        Assert.True(r.AckFailed);
        Assert.True(r.StartGame);
        string notice = MainViewModel.QuickbarNoticeText(r);
        Assert.Contains("Quickbars set up for Fine.", notice);
        Assert.Contains("Quickbars for Locked could not be written", notice);
        Assert.Contains("did not confirm", notice);
    }

    [Fact]
    public void Slots_the_client_cannot_read_are_skipped()
    {
        var c = QuickbarSamples.Character("Asdasd", IdA, (1, 1, 1, 35, 8, "Lunge"), (1, 1, 2, 10, 5, "Bad, Name"), (1, 11, 1, 10, 5, "Bank 11"));
        var details = new List<string>();
        var bars = QuickbarService.ToBars(c, details);
        Assert.Equal([new IniSlot(0, 35, 8, "Lunge")], bars[1]);
        Assert.Equal(2, details.Count);
    }
}
