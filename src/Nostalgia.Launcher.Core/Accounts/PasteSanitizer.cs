namespace Nostalgia.Launcher.Core.Accounts;

/// <summary>Cleans values pasted from Discord: trims whitespace incl. line breaks, NBSP and zero-width characters at both ends.</summary>
public static class PasteSanitizer
{
    private static readonly char[] Extra = ['​', '‌', '‍', '⁠', '﻿'];

    public static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        int start = 0, end = value.Length - 1;
        while (start <= end && IsTrim(value[start])) start++;
        while (end >= start && IsTrim(value[end])) end--;
        return value[start..(end + 1)];
    }

    private static bool IsTrim(char c) => char.IsWhiteSpace(c) || Array.IndexOf(Extra, c) >= 0;
}

/// <summary>Stored login. The password is the random one from the Discord bot.</summary>
public sealed record LoginCredential(string Account, string Password)
{
    /// <summary>Server rule (LoginRequestHandler): letters and digits only, not empty.</summary>
    public static bool IsValidAccountName(string name) =>
        name.Length > 0 && name.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9');

    public override string ToString() => $"LoginCredential({Account}, ***)";
}
