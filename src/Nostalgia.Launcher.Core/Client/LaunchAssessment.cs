namespace Nostalgia.Launcher.Core.Client;

public enum LaunchOutcome
{
    /// <summary>Game ran longer than the early-exit window: normal session.</summary>
    Played,
    /// <summary>Game closed within the window – most likely a refused login (see the hint panel).</summary>
    EarlyExit,
    /// <summary>No game process appeared after the connect tool started.</summary>
    NeverStarted,
}

/// <summary>
/// After "Spielen": a client that closes within <see cref="EarlyExitWindow"/> most likely got a login refusal
/// (wrong password, account unknown / not linked to Discord, server offline – docs/client/launching.md). The client
/// shows the refusal in a message box and closes once it is confirmed, so the window allows time to read it.
/// </summary>
public static class LaunchAssessment
{
    public static readonly TimeSpan EarlyExitWindow = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(45);

    public static LaunchOutcome Assess(bool gameStarted, TimeSpan? runtime) =>
        !gameStarted ? LaunchOutcome.NeverStarted
        : runtime is { } r && r < EarlyExitWindow ? LaunchOutcome.EarlyExit
        : LaunchOutcome.Played;
}
