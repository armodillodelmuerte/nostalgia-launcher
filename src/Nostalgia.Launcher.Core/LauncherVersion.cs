using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Nostalgia.Launcher.Core;

/// <summary>
/// Launcher version <c>major.minor.patch</c> with an optional pre-release suffix (<c>0.2.0-rc1</c>).
/// A pre-release sorts before its release; pre-release labels compare ordinally. A leading "v" is accepted.
/// </summary>
public readonly record struct LauncherVersion(int Major, int Minor, int Patch, string? PreRelease = null)
    : IComparable<LauncherVersion>
{
    public static LauncherVersion Parse(string s) =>
        TryParse(s, out var v) ? v : throw new FormatException($"Not a launcher version: '{s}'");

    public static bool TryParse(string? s, [NotNullWhen(true)] out LauncherVersion v)
    {
        v = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];

        // Drop build metadata (+…).
        int plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];

        string? pre = null;
        int dash = s.IndexOf('-');
        if (dash >= 0)
        {
            pre = s[(dash + 1)..];
            s = s[..dash];
            if (pre.Length == 0) return false;
        }

        var parts = s.Split('.');
        if (parts.Length is < 2 or > 4) return false;
        var nums = new int[3];
        for (int i = 0; i < Math.Min(parts.Length, 3); i++)
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out nums[i])) return false;
        // A fourth part (assembly-style 0.1.0.0) must be numeric and is ignored.
        if (parts.Length == 4 && !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out _)) return false;

        v = new LauncherVersion(nums[0], nums[1], nums[2], pre);
        return true;
    }

    public int CompareTo(LauncherVersion other)
    {
        int c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;
        if (PreRelease is null) return other.PreRelease is null ? 0 : 1;
        if (other.PreRelease is null) return -1;
        return string.CompareOrdinal(PreRelease, other.PreRelease);
    }

    public static bool operator <(LauncherVersion a, LauncherVersion b) => a.CompareTo(b) < 0;
    public static bool operator >(LauncherVersion a, LauncherVersion b) => a.CompareTo(b) > 0;
    public static bool operator <=(LauncherVersion a, LauncherVersion b) => a.CompareTo(b) <= 0;
    public static bool operator >=(LauncherVersion a, LauncherVersion b) => a.CompareTo(b) >= 0;

    public override string ToString() => PreRelease is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{PreRelease}";
}
