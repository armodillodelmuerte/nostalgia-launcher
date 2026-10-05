namespace Nostalgia.Launcher.Core.Quickbars;

/// <summary>
/// Quickbar import from a Master Trainer code (GET http://&lt;host&gt;:&lt;quickbarPort&gt;/quickbar/&lt;code&gt;, merge into
/// the character INI). NOT BUILT YET: it waits for the calibration (docs/client/quickbar-ini.md) and the server
/// endpoint. The tab is shown only when the manifest flag <c>features.quickbars</c> is on AND
/// <see cref="IsAvailable"/> is true, so the flag can't switch on a module this build doesn't have.
/// </summary>
public interface IQuickbarService
{
    bool IsAvailable { get; }
}

/// <summary>Placeholder until the quickbar module exists.</summary>
public sealed class QuickbarServiceUnavailable : IQuickbarService
{
    public bool IsAvailable => false;
}

public static class QuickbarFeature
{
    public static bool Visible(bool manifestFlag, IQuickbarService service) => manifestFlag && service.IsAvailable;
}
