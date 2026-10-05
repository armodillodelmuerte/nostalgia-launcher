using System.Text;

namespace Nostalgia.Launcher.Core.Support;

/// <summary>Input for "Support-Info kopieren". Has no password field on purpose.</summary>
public sealed record SupportInfoData
{
    public required string LauncherVersion { get; init; }
    public required string ArtVersion { get; init; }
    public required string Platform { get; init; }
    public required string OsDescription { get; init; }
    public string? PhaseId { get; init; }
    public int? PhaseNoticeVersion { get; init; }
    public required string ManifestSource { get; init; }
    public string? ManifestError { get; init; }
    public string? ServerHost { get; init; }
    public int? ServerPort { get; init; }
    public string? ServerStatus { get; init; }
    public string? ClientFolder { get; init; }
    public string? ClientDll { get; init; }
    public string? ClientVersion { get; init; }
    public string? ClientStatus { get; init; }
    public string? AccountName { get; init; }
    public required string CredentialStore { get; init; }
    public required string DataFolder { get; init; }
    public required string LogFolder { get; init; }
    public IReadOnlyList<string> RecentErrors { get; init; } = [];
}

public static class SupportInfo
{
    public static string Build(SupportInfoData d, DateTimeOffset now)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Nostalgia Launcher – Support-Info");
        sb.AppendLine($"Zeit: {now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"Launcher: {d.LauncherVersion} | Art: {d.ArtVersion} | Plattform: {d.Platform} ({d.OsDescription})");
        sb.AppendLine($"Phase: {(d.PhaseId is null ? "keine" : $"{d.PhaseId} (Hinweis v{d.PhaseNoticeVersion})")}");
        sb.AppendLine($"Manifest: {d.ManifestSource}{(d.ManifestError is null ? "" : $" – Fehler: {d.ManifestError}")}");
        sb.AppendLine($"Server: {d.ServerHost ?? "-"}:{d.ServerPort?.ToString() ?? "-"} – {d.ServerStatus ?? "unbekannt"}");
        sb.AppendLine($"Client: {d.ClientFolder ?? "-"} | {d.ClientDll ?? "-"} {d.ClientVersion ?? ""} | {d.ClientStatus ?? "-"}");
        sb.AppendLine($"Konto: {d.AccountName ?? "-"} (Passwort wird nie angezeigt) | Speicher: {d.CredentialStore}");
        sb.AppendLine($"Daten: {d.DataFolder}");
        sb.AppendLine($"Logs: {d.LogFolder}");
        sb.AppendLine("Letzte Fehler:");
        if (d.RecentErrors.Count == 0) sb.AppendLine("  (keine)");
        foreach (var e in d.RecentErrors) sb.AppendLine("  " + e);
        return sb.ToString();
    }
}
