using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nostalgia.Launcher.Core.Accounts;

namespace Nostalgia.Launcher.Core.Quickbars;

// Server side: core custom/Logic/Quickbar/QuickbarLauncherApi.cs (OpenDAoC, Nostalgia). formatVersion 1.

public sealed record PendingSlot
{
    public int Bank { get; init; }
    public int Slot { get; init; }
    public int Hotkey { get; init; }
    public int Type { get; init; }
    public int Value { get; init; }
    public string Name { get; init; } = "";
}

public sealed record PendingCharacter
{
    public string PendingId { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Realm { get; init; }
    public string? Class { get; init; }
    public string? Preset { get; init; }
    public string? Weapon { get; init; }
    public string? CreatedUtc { get; init; }
    /// <summary>"1", "2", "3" → slots of that bar.</summary>
    public Dictionary<string, List<PendingSlot>> Bars { get; init; } = [];
}

public sealed record PendingResponse
{
    public int FormatVersion { get; init; }
    public List<PendingCharacter> Characters { get; init; } = [];
}

public enum QuickbarApiError { Unavailable, LoginFailed }

public sealed class QuickbarApiException(QuickbarApiError error, string message, Exception? inner = null) : Exception(message, inner)
{
    public QuickbarApiError Error { get; } = error;
}

/// <summary>The server's launcher endpoint. Every failure is a <see cref="QuickbarApiException"/>.</summary>
public interface IQuickbarApi
{
    Task<PendingResponse> PendingAsync(string baseUrl, LoginCredential login, string launcherVersion, CancellationToken ct);
    Task<int> AckAsync(string baseUrl, LoginCredential login, IReadOnlyList<string> pendingIds, CancellationToken ct);
}

/// <summary>
/// <c>POST &lt;base&gt;/quickbar/pending {account, password, launcherVersion}</c> and <c>POST &lt;base&gt;/quickbar/ack {account, password,
/// pendingIds}</c>. HTTPS only; plain HTTP only to this machine (staging), like the self-update. 5 s per request.
/// </summary>
public sealed class QuickbarHttpApi(HttpClient http) : IQuickbarApi
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    public const int SupportedFormatVersion = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>HTTPS, or HTTP to a loopback address (staging). No query or fragment.</summary>
    public static bool IsAllowedBaseUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u)
        && (u.Scheme == Uri.UriSchemeHttps || (u.Scheme == Uri.UriSchemeHttp && u.IsLoopback))
        && string.IsNullOrEmpty(u.Query) && string.IsNullOrEmpty(u.Fragment) && string.IsNullOrEmpty(u.UserInfo);

    public async Task<PendingResponse> PendingAsync(string baseUrl, LoginCredential login, string launcherVersion, CancellationToken ct)
    {
        var body = new { account = login.Account, password = login.Password, launcherVersion };
        var reply = await PostAsync<PendingResponse>(baseUrl, "pending", body, ct);
        if (reply.FormatVersion != SupportedFormatVersion)
            throw new QuickbarApiException(QuickbarApiError.Unavailable, $"formatVersion {reply.FormatVersion} not supported");
        return reply;
    }

    public async Task<int> AckAsync(string baseUrl, LoginCredential login, IReadOnlyList<string> pendingIds, CancellationToken ct)
    {
        var body = new { account = login.Account, password = login.Password, pendingIds };
        return (await PostAsync<AckResponse>(baseUrl, "ack", body, ct)).Acknowledged;
    }

    private sealed record AckResponse(int Acknowledged);

    private async Task<T> PostAsync<T>(string baseUrl, string action, object body, CancellationToken ct)
    {
        if (!IsAllowedBaseUrl(baseUrl))
            throw new QuickbarApiException(QuickbarApiError.Unavailable, "quickbarUrl not allowed (HTTPS only, HTTP only to localhost)");
        var url = new Uri(baseUrl.TrimEnd('/') + "/quickbar/" + action);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        try
        {
            // Sized body: the server's HTTP parser accepts Content-Length only and answers 501 to a chunked body
            // (PostAsJsonAsync streams with Transfer-Encoding: chunked).
            using var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body, Json));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            using var resp = await http.PostAsync(url, content, cts.Token);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                throw new QuickbarApiException(QuickbarApiError.LoginFailed, "login failed (401)");
            if (!resp.IsSuccessStatusCode)
                throw new QuickbarApiException(QuickbarApiError.Unavailable, $"HTTP {(int)resp.StatusCode}");
            return await resp.Content.ReadFromJsonAsync<T>(Json, cts.Token)
                   ?? throw new QuickbarApiException(QuickbarApiError.Unavailable, "empty answer");
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new QuickbarApiException(QuickbarApiError.Unavailable, $"no answer within {Timeout.TotalSeconds:0} s", e);
        }
        catch (HttpRequestException e)
        {
            throw new QuickbarApiException(QuickbarApiError.Unavailable, e.Message, e);
        }
        catch (JsonException e)
        {
            throw new QuickbarApiException(QuickbarApiError.Unavailable, "invalid answer: " + e.Message, e);
        }
    }
}
