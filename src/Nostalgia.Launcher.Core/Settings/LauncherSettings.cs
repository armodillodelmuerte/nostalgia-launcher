using System.Text.Json;

namespace Nostalgia.Launcher.Core.Settings;

/// <summary>Local settings (<c>settings.json</c> in the app data folder). Never contains the password.</summary>
public sealed class LauncherSettings
{
    public string? ClientFolder { get; set; }
    /// <summary>game.dll file name chosen in <see cref="ClientFolder"/> (e.g. game1127.dll).</summary>
    public string? ClientDll { get; set; }
    /// <summary>Highest acknowledged notice version per phase id ("beta" → 1).</summary>
    public Dictionary<string, int> AcknowledgedNotices { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool RememberLogin { get; set; } = true;
}

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public string Path => path;

    public LauncherSettings Load()
    {
        try
        {
            if (File.Exists(path))
            {
                var s = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(path), Json);
                if (s is not null)
                {
                    s.AcknowledgedNotices = new(s.AcknowledgedNotices ?? [], StringComparer.OrdinalIgnoreCase);
                    return s;
                }
            }
        }
        catch (JsonException) { }
        catch (IOException) { }
        return new LauncherSettings();
    }

    public void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Json));
        File.Move(tmp, path, overwrite: true);
    }
}
