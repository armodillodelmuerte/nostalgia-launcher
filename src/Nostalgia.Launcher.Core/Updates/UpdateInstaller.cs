using System.Diagnostics;

namespace Nostalgia.Launcher.Core.Updates;

/// <summary>
/// Self-update in two steps, no second binary needed:
/// 1. <see cref="StageAsync"/>: the running launcher downloads the new exe into the update folder and checks its SHA-256.
///    Only a verified file is ever started.
/// 2. The app starts the staged exe with <c>--apply-update --target &lt;old exe&gt; --pid &lt;old pid&gt;</c> and exits.
///    The staged exe (= the small updater step) runs <see cref="ApplyAsync"/>: waits for the old process, copies itself
///    over the old exe, then the app starts the replaced exe with <c>--updated</c>, which removes the update folder.
/// </summary>
public sealed class UpdateInstaller(HttpClient http, string updateDir)
{
    public string UpdateDir => updateDir;

    public async Task<string> StageAsync(UpdatePlan plan, string exeFileName, IProgress<double>? progress, CancellationToken ct)
    {
        if (plan.Target is null || plan.DownloadUrl is null || plan.Sha256 is null)
            throw new InvalidOperationException("No update target.");
        if (!UpdateVerifier.IsAllowedDownloadUrl(plan.DownloadUrl))
            throw new InvalidOperationException("Download URL not allowed (HTTPS only).");

        Directory.CreateDirectory(updateDir);
        string part = Path.Combine(updateDir, $"download-{plan.Target}.part");
        string staged = Path.Combine(updateDir, $"{plan.Target}-{exeFileName}");

        using (var resp = await http.GetAsync(plan.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            long? total = resp.Content.Headers.ContentLength;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(part);
            var buf = new byte[81920];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                done += n;
                if (total > 0) progress?.Report((double)done / total.Value);
            }
        }

        if (!await UpdateVerifier.VerifyAsync(part, plan.Sha256, ct))
        {
            TryDelete(part);
            throw new UpdateVerificationException(plan.Target.Value.ToString());
        }

        File.Move(part, staged, overwrite: true);
        return staged;
    }

    /// <summary>Runs inside the staged exe: wait for the old launcher to exit, then copy <paramref name="stagedExe"/> over <paramref name="targetExe"/>.</summary>
    public static async Task ApplyAsync(string stagedExe, string targetExe, int oldPid, TimeSpan waitLimit, CancellationToken ct = default)
    {
        try
        {
            using var old = Process.GetProcessById(oldPid);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(waitLimit);
            await old.WaitForExitAsync(cts.Token);
        }
        catch (ArgumentException) { /* already gone */ }
        catch (InvalidOperationException) { }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { /* try anyway, the copy retries */ }

        Exception? last = null;
        for (int i = 0; i < 40; i++)
        {
            try
            {
                File.Copy(stagedExe, targetExe, overwrite: true);
                return;
            }
            catch (IOException e) { last = e; }
            catch (UnauthorizedAccessException e) { last = e; }
            await Task.Delay(250, ct);
        }
        throw new IOException($"Could not replace {targetExe}", last);
    }

    /// <summary>Called by the replaced launcher after <c>--updated</c>: removes staged files (may still be running for a moment).</summary>
    public async Task CleanupAsync()
    {
        for (int i = 0; i < 20 && Directory.Exists(updateDir); i++)
        {
            try { Directory.Delete(updateDir, recursive: true); return; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            await Task.Delay(500);
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

public sealed class UpdateVerificationException(string version)
    : Exception($"SHA-256 of the downloaded launcher {version} does not match the manifest.");
