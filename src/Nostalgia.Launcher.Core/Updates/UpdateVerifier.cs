using System.Security.Cryptography;

namespace Nostalgia.Launcher.Core.Updates;

public static class UpdateVerifier
{
    public static bool IsSha256Hex(string? s) =>
        s is { Length: 64 } && s.All(Uri.IsHexDigit);

    /// <summary>Downloads only over HTTPS; plain HTTP only to this machine (local update tests).</summary>
    public static bool IsAllowedDownloadUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme == Uri.UriSchemeHttps) return true;
        return uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
    }

    public static async Task<string> Sha256OfFileAsync(string path, CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(fs, ct);
        return Convert.ToHexString(hash);
    }

    public static async Task<bool> VerifyAsync(string path, string expectedSha256, CancellationToken ct = default)
    {
        if (!IsSha256Hex(expectedSha256) || !File.Exists(path)) return false;
        string actual = await Sha256OfFileAsync(path, ct);
        return string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
