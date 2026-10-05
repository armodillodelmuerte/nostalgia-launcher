using Nostalgia.Launcher.Core.Manifest;

namespace Nostalgia.Launcher.Core.Updates;

public enum UpdateKind
{
    /// <summary>Up to date (or the manifest offers nothing newer – never downgrade).</summary>
    None,
    /// <summary>A newer version exists; playing stays possible.</summary>
    Optional,
    /// <summary>This build is below <c>minimumVersion</c>: update before playing.</summary>
    Required,
    /// <summary>Below the minimum, but the manifest offers no newer download (manifest error).</summary>
    RequiredUnavailable,
}

public sealed record UpdatePlan(UpdateKind Kind, LauncherVersion Current, LauncherVersion? Target, string? DownloadUrl, string? Sha256);

public static class UpdatePlanner
{
    public static UpdatePlan Plan(LauncherVersion current, LauncherRelease release)
    {
        LauncherVersion.TryParse(release.LatestVersion, out var latest);
        LauncherVersion.TryParse(release.MinimumVersion, out var minimum);

        bool newer = latest > current
                     && !string.IsNullOrWhiteSpace(release.DownloadUrl)
                     && UpdateVerifier.IsSha256Hex(release.Sha256)
                     && UpdateVerifier.IsAllowedDownloadUrl(release.DownloadUrl!);
        bool belowMinimum = current < minimum;

        if (newer)
            return new(belowMinimum ? UpdateKind.Required : UpdateKind.Optional, current, latest, release.DownloadUrl, release.Sha256);
        return new(belowMinimum ? UpdateKind.RequiredUnavailable : UpdateKind.None, current, null, null, null);
    }
}
