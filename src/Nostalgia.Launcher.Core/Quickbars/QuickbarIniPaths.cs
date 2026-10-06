using System.Text;

namespace Nostalgia.Launcher.Core.Quickbars;

/// <summary>
/// Where the 1.127 client keeps a character's INI (docs/client/quickbar-ini.md):
/// <c>%APPDATA%\Electronic Arts\Dark Age of Camelot\&lt;settings&gt;\&lt;Character&gt;-5.ini</c>.
/// <c>&lt;settings&gt;</c> comes from <c>&lt;client folder&gt;\paths.dat</c>; 5 is the server ID every OpenDAoC server sends, so a
/// same-named character of another OpenDAoC server shares the file – there is no way to tell them apart.
/// </summary>
public static class QuickbarIniPaths
{
    /// <summary>Server ID of OpenDAoC's LoginGranted packet (hard-coded 0x05); part of the file name.</summary>
    public const int ServerId = 5;

    /// <summary>The client reads the value into a 63-byte buffer (62 characters + NUL).</summary>
    private const int MaxValueLength = 62;

    /// <summary>
    /// The <c>settings</c> value of <c>[paths]</c> as the client sees it, or null if missing/empty. The client's INI reader opens the
    /// file in text mode and always overwrites the last character of a line with NUL (it expects <c>\n</c>): the installer's
    /// <c>[paths]\r\nsettings=Atlas1</c> without a final line break gives <c>Atlas</c>; with a line break the full value is used.
    /// </summary>
    public static string? SettingsFolderName(byte[] pathsDat)
    {
        string text = Encoding.Latin1.GetString(pathsDat).Replace("\r\n", "\n");
        bool inPaths = false;
        int start = 0;
        while (start < text.Length)
        {
            int nl = text.IndexOf('\n', start);
            // Line incl. its '\n'; the last line may have none. The client drops the last character either way.
            string raw = nl < 0 ? text[start..] : text[start..(nl + 1)];
            start = nl < 0 ? text.Length : nl + 1;
            string line = raw.Length > 0 ? raw[..^1] : raw;

            string t = line.Trim();
            if (t.StartsWith('[') && t.EndsWith(']'))
            {
                inPaths = string.Equals(t[1..^1].Trim(), "paths", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            int eq = line.IndexOf('=');
            if (!inPaths || eq < 0 || !string.Equals(line[..eq].Trim(), "settings", StringComparison.OrdinalIgnoreCase)) continue;
            string value = line[(eq + 1)..];
            if (value.Length > MaxValueLength) value = value[..MaxValueLength];
            value = value.Trim();
            return value.Length == 0 || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value is "." or ".." ? null : value;
        }
        return null;
    }

    /// <summary><c>&lt;Name&gt;-5.ini</c>; the client uses the text before a '-' (our names have none).</summary>
    public static string FileName(string characterName) => $"{characterName.Split('-')[0]}-{ServerId}.ini";

    /// <summary>The settings folder for a client install: <paramref name="daocRoot"/> + the paths.dat value; null if paths.dat is missing or has no value.</summary>
    public static string? SettingsFolder(string daocRoot, string clientFolder)
    {
        string pathsDat = Path.Combine(clientFolder, "paths.dat");
        if (!File.Exists(pathsDat)) return null;
        string? name = SettingsFolderName(File.ReadAllBytes(pathsDat));
        return name is null ? null : Path.Combine(daocRoot, name);
    }
}
