using System.Globalization;
using System.Text;

namespace Nostalgia.Launcher.Core.Quickbars;

/// <summary>One slot to write: <c>Hotkey_&lt;Hotkey&gt;=&lt;Type&gt;,&lt;Value&gt;,&lt;Name&gt;,0</c> (the client fills in the icon).</summary>
public sealed record IniSlot(int Hotkey, int Type, int Value, string Name);

/// <summary>
/// Replaces the quickbars of a 1.127 character INI (<c>&lt;Character&gt;-5.ini</c>, docs/client/quickbar-ini.md) and leaves
/// everything else as it was: no INI library, the file is edited line by line.
/// <list type="bullet">
/// <item>Bars 1–3 = sections <c>[Quickbar]</c>, <c>[Quickbar2]</c>, <c>[Quickbar3]</c>: every <c>Hotkey_N</c> line is removed (all
///   banks, all entry types – items and macros too), then the layout's lines are added at the end of the section. Other keys of
///   those sections (<c>GroupSize</c>) stay. A missing section is created (with <c>GroupSize=10</c>) only if it gets slots.</item>
/// <item><c>[Panels]</c> <c>Quickbar2=</c>/<c>Quickbar3=</c>: fields 3 and 7 set to 1 when that bar gets slots (shows the bar;
///   the calibration could not tell the two flags apart, both were 0 for a hidden bar). Missing line = left to the client.</item>
/// <item>All other lines keep their text, order and line ending. Bytes are read and written as Latin-1, so every byte of the
///   file survives unchanged (the client writes ANSI); a UTF-8 BOM stays where it was.</item>
/// </list>
/// </summary>
public static class QuickbarIniWriter
{
    public const int Bars = 3;
    public const int MaxHotkey = 99;
    private const string HotkeyPrefix = "Hotkey_";
    private const string Bom = "ï»¿"; // UTF-8 BOM bytes decoded as Latin-1

    public static string SectionName(int bar) => bar == 1 ? "Quickbar" : "Quickbar" + bar.ToString(CultureInfo.InvariantCulture);

    /// <summary>A name the client can read back: not empty, no field/line separators, Latin-1 only.</summary>
    public static bool IsWritableName(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= 128 && name.All(c => c is >= ' ' and <= 'ÿ' and not ',' and not '\u007F');

    public static string FormatHotkey(IniSlot s) =>
        string.Create(CultureInfo.InvariantCulture, $"{HotkeyPrefix}{s.Hotkey}={s.Type},{s.Value},{s.Name},0");

    /// <summary>Reads <paramref name="path"/>, applies <paramref name="bars"/> (bar 1–3 → slots) and replaces the file atomically.</summary>
    public static void ApplyToFile(string path, IReadOnlyDictionary<int, IReadOnlyList<IniSlot>> bars)
    {
        string text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
        string result = Apply(text, bars);
        string tmp = path + ".nostalgia.tmp";
        File.WriteAllBytes(tmp, Encoding.Latin1.GetBytes(result));
        File.Move(tmp, path, overwrite: true);
    }

    public static string Apply(string text, IReadOnlyDictionary<int, IReadOnlyList<IniSlot>> bars)
    {
        foreach (var (bar, slots) in bars)
        {
            if (bar is < 1 or > Bars) throw new ArgumentOutOfRangeException(nameof(bars), $"bar {bar}");
            foreach (var s in slots)
            {
                if (s.Hotkey is < 0 or > MaxHotkey) throw new ArgumentOutOfRangeException(nameof(bars), $"hotkey {s.Hotkey}");
                if (!IsWritableName(s.Name)) throw new ArgumentException($"name '{s.Name}' can't be written", nameof(bars));
            }
        }

        var lines = Split(text);
        string eol = lines.Select(l => l.Eol).FirstOrDefault(e => e.Length > 0) ?? "\r\n";
        bool HasSlots(int bar) => bars.TryGetValue(bar, out var s) && s.Count > 0;

        // Pass 1: drop every Hotkey_ line of bars 1–3, show bars 2/3 in [Panels].
        var kept = new List<Line>(lines.Count);
        string? section = null;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            string body = i == 0 && line.Text.StartsWith(Bom, StringComparison.Ordinal) ? line.Text[Bom.Length..] : line.Text;
            if (SectionOf(body) is { } header)
            {
                section = header;
                kept.Add(line);
                continue;
            }

            string key = KeyOf(body);
            if (BarOf(section) > 0 && key.StartsWith(HotkeyPrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(section, "Panels", StringComparison.OrdinalIgnoreCase) && BarOf(key) is var pb and > 1 && HasSlots(pb))
                line = line with { Text = ShowBar(line.Text) };
            kept.Add(line);
        }

        // Pass 2: add the layout's lines to the first section of each bar (created if missing).
        for (int bar = 1; bar <= Bars; bar++)
        {
            if (!HasSlots(bar)) continue;
            var add = bars[bar].OrderBy(s => s.Hotkey).Select(s => new Line(FormatHotkey(s), eol)).ToList();
            int header = FindSection(kept, bar);
            if (header < 0)
            {
                add.Insert(0, new Line("GroupSize=10", eol));
                add.Insert(0, new Line($"[{SectionName(bar)}]", eol));
                Insert(kept, NewSectionPosition(kept, bar), add, eol);
            }
            else
            {
                Insert(kept, EndOfContent(kept, header), add, eol);
            }
        }

        var sb = new StringBuilder(text.Length + 512);
        foreach (var l in kept) sb.Append(l.Text).Append(l.Eol);
        return sb.ToString();
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────

    private readonly record struct Line(string Text, string Eol);

    private static List<Line> Split(string text)
    {
        var result = new List<Line>();
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\r' && text[i] != '\n') continue;
            int len = text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
            result.Add(new Line(text[start..i], text.Substring(i, len)));
            i += len - 1;
            start = i + 1;
        }
        if (start < text.Length) result.Add(new Line(text[start..], ""));
        return result;
    }

    private static string? SectionOf(string body)
    {
        string t = body.Trim();
        return t.Length >= 2 && t[0] == '[' && t[^1] == ']' ? t[1..^1].Trim() : null;
    }

    private static string KeyOf(string body)
    {
        int eq = body.IndexOf('=');
        return (eq < 0 ? body : body[..eq]).Trim();
    }

    private static int BarOf(string? section)
    {
        for (int bar = 1; bar <= Bars; bar++)
            if (string.Equals(section, SectionName(bar), StringComparison.OrdinalIgnoreCase)) return bar;
        return 0;
    }

    /// <summary><c>QuickbarX=x,y,a,1,alpha,bank,b</c>: a (field 3) and b (field 7) = 1. Lines with fewer fields stay as they are.</summary>
    private static string ShowBar(string text)
    {
        int eq = text.IndexOf('=');
        var fields = text[(eq + 1)..].Split(',');
        if (fields.Length < 7) return text;
        fields[2] = "1";
        fields[6] = "1";
        return text[..(eq + 1)] + string.Join(',', fields);
    }

    private static int FindSection(List<Line> lines, int bar)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string body = i == 0 && lines[i].Text.StartsWith(Bom, StringComparison.Ordinal) ? lines[i].Text[Bom.Length..] : lines[i].Text;
            if (BarOf(SectionOf(body)) == bar) return i;
        }
        return -1;
    }

    private static int NextHeader(List<Line> lines, int header)
    {
        for (int i = header + 1; i < lines.Count; i++)
            if (SectionOf(lines[i].Text) is not null) return i;
        return lines.Count;
    }

    /// <summary>After the last non-blank line of the section (blank lines before the next header stay below the new lines).</summary>
    private static int EndOfContent(List<Line> lines, int header)
    {
        int end = NextHeader(lines, header);
        while (end - 1 > header && string.IsNullOrWhiteSpace(lines[end - 1].Text)) end--;
        return end;
    }

    /// <summary>A new bar section goes after the nearest lower bar's section, else before the nearest higher one, else at the end.</summary>
    private static int NewSectionPosition(List<Line> lines, int bar)
    {
        for (int lower = bar - 1; lower >= 1; lower--)
            if (FindSection(lines, lower) is var h and >= 0) return EndOfContent(lines, h);
        for (int higher = bar + 1; higher <= Bars; higher++)
            if (FindSection(lines, higher) is var h and >= 0) return h;
        return lines.Count;
    }

    private static void Insert(List<Line> lines, int at, List<Line> add, string eol)
    {
        // The line before the insert point must end with a line break; if it was the file's last line without one,
        // the inserted block takes over "no line break at the end".
        if (at > 0 && lines[at - 1].Eol.Length == 0)
        {
            lines[at - 1] = lines[at - 1] with { Eol = eol };
            if (at == lines.Count) add[^1] = add[^1] with { Eol = "" };
        }
        lines.InsertRange(at, add);
    }
}
