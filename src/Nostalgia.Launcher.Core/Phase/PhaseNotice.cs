using Nostalgia.Launcher.Core.Manifest;
using Nostalgia.Launcher.Core.Settings;

namespace Nostalgia.Launcher.Core.Phase;

/// <summary>
/// Phase UI rules, driven only by the manifest's <c>phase</c> block: badge, window title, footer strip and the notice
/// dialog. The dialog shows on first start and again whenever <c>noticeVersion</c> goes up (stored per phase id).
/// </summary>
public static class PhaseNotice
{
    public static bool HasPhaseUi(PhaseInfo? phase) => phase is not null && !string.IsNullOrWhiteSpace(phase.Id);

    public static bool ShouldShowDialog(PhaseInfo? phase, LauncherSettings settings)
    {
        if (!HasPhaseUi(phase)) return false;
        return !settings.AcknowledgedNotices.TryGetValue(phase!.Id, out int seen) || seen < phase.NoticeVersion;
    }

    public static void Acknowledge(PhaseInfo phase, LauncherSettings settings)
    {
        settings.AcknowledgedNotices.TryGetValue(phase.Id, out int seen);
        settings.AcknowledgedNotices[phase.Id] = Math.Max(seen, phase.NoticeVersion);
    }

    /// <summary>Window title: fixed wording for the beta, the phase title for other phases, plain name without phase.</summary>
    public static string WindowTitle(PhaseInfo? phase, string betaTitle, string plainTitle) =>
        !HasPhaseUi(phase) ? plainTitle
        : string.Equals(phase!.Id, "beta", StringComparison.OrdinalIgnoreCase) ? betaTitle
        : $"{plainTitle} – {phase.Title}";
}
