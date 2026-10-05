namespace Nostalgia.Launcher.Services;

/// <summary>Window-bound actions the view model needs (implemented by the main window; faked in UI tests).</summary>
public interface IUiShell
{
    Task OpenUrlAsync(string url);
    Task OpenFolderAsync(string folder);
    Task<string?> PickFolderAsync(string title);
    Task CopyTextAsync(string text);
    /// <summary>Starts the staged update exe and closes the launcher.</summary>
    void RestartInto(string exePath, IReadOnlyList<string> args);
}
