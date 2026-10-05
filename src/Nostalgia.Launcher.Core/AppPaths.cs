namespace Nostalgia.Launcher.Core;

/// <summary>Local data folder: Windows %LOCALAPPDATA%\Nostalgia, Linux ~/.local/share/Nostalgia. Override: NOSTALGIA_DATA_DIR (tests).</summary>
public sealed class AppPaths(string root)
{
    public static AppPaths Default()
    {
        string? overrideDir = Environment.GetEnvironmentVariable("NOSTALGIA_DATA_DIR");
        string root = !string.IsNullOrWhiteSpace(overrideDir)
            ? overrideDir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nostalgia");
        return new AppPaths(root);
    }

    public string Root => root;
    public string Logs => Path.Combine(root, "logs");
    public string Settings => Path.Combine(root, "settings.json");
    public string ManifestCache => Path.Combine(root, "manifest-cache.json");
    public string Login => Path.Combine(root, "login.dat");
    public string Updates => Path.Combine(root, "update");
}
