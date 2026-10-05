namespace Nostalgia.Launcher.Core.Manifest;

public enum ManifestSource
{
    Remote,
    /// <summary>Remote failed, last good copy from disk.</summary>
    Cache,
    /// <summary>No manifest was ever loaded: <see cref="BuiltInDefaults.Fallback"/>.</summary>
    BuiltIn,
}

public sealed record ManifestLoadResult(LauncherManifest Manifest, ManifestSource Source, string Location, string? RemoteError);

/// <summary>Fetches the raw manifest text (HTTP or a local file for tests).</summary>
public interface IManifestFetcher
{
    Task<string> FetchAsync(string location, CancellationToken ct);
}

public sealed class HttpManifestFetcher(HttpClient http) : IManifestFetcher
{
    public async Task<string> FetchAsync(string location, CancellationToken ct)
    {
        if (File.Exists(location))
            return await File.ReadAllTextAsync(location, ct);

        var uri = new Uri(location);
        if (uri.IsFile)
            return await File.ReadAllTextAsync(uri.LocalPath, ct);

        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        req.Headers.CacheControl = new() { NoCache = true };
        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }
}

/// <summary>Remote manifest → on success cached to disk; on failure the cached copy; never loaded → built-in fallback.</summary>
public sealed class ManifestService(IManifestFetcher fetcher, string cachePath, LauncherVersion buildVersion)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    public async Task<ManifestLoadResult> LoadAsync(string location, CancellationToken ct = default)
    {
        string? error;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);
            string json = await fetcher.FetchAsync(location, cts.Token);
            var parsed = ManifestParser.Parse(json);
            if (parsed.Ok)
            {
                TryWriteCache(json);
                return new(parsed.Manifest!, ManifestSource.Remote, location, null);
            }
            error = "invalid manifest: " + parsed.Error;
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            error = e.GetType().Name + ": " + e.Message;
        }

        var cached = TryReadCache();
        if (cached is not null)
            return new(cached, ManifestSource.Cache, cachePath, error);

        return new(BuiltInDefaults.Fallback(buildVersion), ManifestSource.BuiltIn, "built-in", error);
    }

    private LauncherManifest? TryReadCache()
    {
        try
        {
            if (!File.Exists(cachePath)) return null;
            var parsed = ManifestParser.Parse(File.ReadAllText(cachePath));
            return parsed.Manifest;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private void TryWriteCache(string json)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            string tmp = cachePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, cachePath, overwrite: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
