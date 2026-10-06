using System.Text.RegularExpressions;
using Nostalgia.Launcher.Core.Accounts;
using Nostalgia.Launcher.Core.Platform;

namespace Nostalgia.Launcher.Core.Quickbars;

/// <summary>
/// Quickbar step of Play: layouts the Master Trainer stored for this account's characters are written into their INIs
/// before the client starts. Runs only when the manifest flag <c>features.quickbars</c> is on, <c>server.quickbarUrl</c> is set
/// and the build has the module (<see cref="IsAvailable"/>).
/// </summary>
public interface IQuickbarService
{
    bool IsAvailable { get; }

    /// <summary>Never throws for server or file problems – they are part of the result; Play goes on unless the result says otherwise.</summary>
    Task<QuickbarPlayResult> BeforePlayAsync(QuickbarPlayRequest request, CancellationToken ct);
}

public sealed record QuickbarPlayRequest(string BaseUrl, LoginCredential Login, string ClientFolder, IReadOnlyList<string> DllNames, string LauncherVersion);

public enum QuickbarOutcome
{
    /// <summary>The server has nothing for this account.</summary>
    NothingPending,
    /// <summary>Server not reachable, timeout, HTTP error, unknown format.</summary>
    Unavailable,
    /// <summary>401 from the server (wrong password, unknown account, ban, no Discord link – the server does not say which).</summary>
    LoginFailed,
    /// <summary>Layouts are pending but a client runs: nothing written, the game is not started.</summary>
    ClientRunning,
    /// <summary>Layouts were pending; see the lists for what happened per character.</summary>
    Processed,
}

public sealed record QuickbarPlayResult(QuickbarOutcome Outcome)
{
    public IReadOnlyList<string> Written { get; init; } = [];
    /// <summary>No INI for these characters yet (never logged in on this PC): kept pending.</summary>
    public IReadOnlyList<string> IniMissing { get; init; } = [];
    /// <summary>The INI could not be written (locked, no access, invalid data): kept pending.</summary>
    public IReadOnlyList<string> WriteFailed { get; init; } = [];
    /// <summary>Written, but /quickbar/ack failed – the server keeps them pending.</summary>
    public bool AckFailed { get; init; }
    /// <summary>Lines for the launcher log (never the password).</summary>
    public IReadOnlyList<string> Details { get; init; } = [];

    public bool StartGame => Outcome != QuickbarOutcome.ClientRunning;
}

public static class QuickbarFeature
{
    public static bool Active(bool manifestFlag, string? quickbarUrl, IQuickbarService service) =>
        manifestFlag && !string.IsNullOrWhiteSpace(quickbarUrl) && service.IsAvailable;
}

/// <summary>A build without the module (tests, platforms that can't write the INI).</summary>
public sealed class QuickbarServiceUnavailable : IQuickbarService
{
    public bool IsAvailable => false;
    public Task<QuickbarPlayResult> BeforePlayAsync(QuickbarPlayRequest request, CancellationToken ct) =>
        Task.FromResult(new QuickbarPlayResult(QuickbarOutcome.NothingPending));
}

/// <summary>
/// The flow: pending → (nothing: done) → refuse while a client runs → per character: find <c>&lt;Name&gt;-5.ini</c> (missing: keep
/// pending), clear bars 1–3 and write the layout → ack only the written ids.
/// </summary>
public sealed partial class QuickbarService(IQuickbarApi api, IQuickbarIniLocator locator, IProcessWatcher processes) : IQuickbarService
{
    public bool IsAvailable => true;

    [GeneratedRegex("^[0-9a-f]{32}$")] private static partial Regex PendingIdRx();
    [GeneratedRegex("^[A-Za-z0-9]{1,40}$")] private static partial Regex CharacterNameRx();

    public async Task<QuickbarPlayResult> BeforePlayAsync(QuickbarPlayRequest r, CancellationToken ct)
    {
        var details = new List<string>();
        PendingResponse pending;
        try
        {
            pending = await api.PendingAsync(r.BaseUrl, r.Login, r.LauncherVersion, ct);
        }
        catch (QuickbarApiException e)
        {
            details.Add($"pending: {e.Message}");
            return new QuickbarPlayResult(e.Error == QuickbarApiError.LoginFailed ? QuickbarOutcome.LoginFailed : QuickbarOutcome.Unavailable)
                { Details = details };
        }

        if (pending.Characters.Count == 0)
            return new QuickbarPlayResult(QuickbarOutcome.NothingPending) { Details = ["pending: none"] };
        details.Add($"pending: {string.Join(", ", pending.Characters.Select(c => $"{c.Name} ({c.Class} {c.Preset}{(string.IsNullOrEmpty(c.Weapon) ? "" : " " + c.Weapon)})"))}");

        if (processes.AnyGameRunning(r.DllNames))
        {
            details.Add("a client is running – nothing written");
            return new QuickbarPlayResult(QuickbarOutcome.ClientRunning) { Details = details };
        }

        string? folder = null;
        try { folder = locator.SettingsFolder(r.ClientFolder); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { details.Add($"settings folder: {e.Message}"); }
        details.Add($"settings folder: {folder ?? "unknown (paths.dat missing or empty)"}");

        var written = new List<string>();
        var writtenIds = new List<string>();
        var missing = new List<string>();
        var failed = new List<string>();
        foreach (var c in pending.Characters)
        {
            if (!PendingIdRx().IsMatch(c.PendingId) || !CharacterNameRx().IsMatch(c.Name))
            {
                details.Add($"skipped an entry with an invalid id or name '{c.Name}'");
                failed.Add(c.Name);
                continue;
            }

            string? ini = folder is null ? null : Path.Combine(folder, QuickbarIniPaths.FileName(c.Name));
            if (ini is null || !File.Exists(ini))
            {
                details.Add($"{c.Name}: no INI ({ini ?? "settings folder unknown"}) – kept pending");
                missing.Add(c.Name);
                continue;
            }

            var bars = ToBars(c, details);
            try
            {
                QuickbarIniWriter.ApplyToFile(ini, bars);
                written.Add(c.Name);
                writtenIds.Add(c.PendingId);
                details.Add($"{c.Name}: wrote {bars.Sum(b => b.Value.Count)} slots into {ini}");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                details.Add($"{c.Name}: {ini} not written ({e.Message}) – kept pending");
                failed.Add(c.Name);
            }
        }

        bool ackFailed = false;
        if (writtenIds.Count > 0)
        {
            try
            {
                int n = await api.AckAsync(r.BaseUrl, r.Login, writtenIds, ct);
                details.Add($"ack: {n} of {writtenIds.Count}");
            }
            catch (QuickbarApiException e)
            {
                ackFailed = true;
                details.Add($"ack failed: {e.Message} – the server keeps them pending");
            }
        }

        return new QuickbarPlayResult(QuickbarOutcome.Processed)
        {
            Written = written, IniMissing = missing, WriteFailed = failed, AckFailed = ackFailed, Details = details,
        };
    }

    /// <summary>Server bars → writer slots. Slots the client could not read back are skipped (logged), the rest is written.</summary>
    internal static Dictionary<int, IReadOnlyList<IniSlot>> ToBars(PendingCharacter c, List<string> details)
    {
        var result = new Dictionary<int, IReadOnlyList<IniSlot>>();
        for (int bar = 1; bar <= QuickbarIniWriter.Bars; bar++)
        {
            var slots = new List<IniSlot>();
            if (c.Bars.TryGetValue(bar.ToString(System.Globalization.CultureInfo.InvariantCulture), out var list))
                foreach (var s in list)
                {
                    bool ok = s.Bank is >= 1 and <= 10 && s.Slot is >= 1 and <= 10 && s.Hotkey == (s.Bank - 1) * 10 + (s.Slot - 1)
                              && s.Type > 0 && s.Value >= 0 && QuickbarIniWriter.IsWritableName(s.Name)
                              && slots.All(x => x.Hotkey != s.Hotkey);
                    if (ok) slots.Add(new IniSlot(s.Hotkey, s.Type, s.Value, s.Name));
                    else details.Add($"{c.Name}: skipped slot bar {bar} bank {s.Bank} slot {s.Slot} '{s.Name}'");
                }
            result[bar] = slots;
        }
        return result;
    }
}
