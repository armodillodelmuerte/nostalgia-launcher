using System.Collections.Concurrent;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Nostalgia.Launcher.Services;

/// <summary>
/// Rolling log files in &lt;data&gt;/logs (daily, 10 files, 5 MB each) plus the last warnings/errors in memory for the
/// support info. Never log passwords: call sites log the account name only.
/// </summary>
public static class AppLog
{
    public static RecentErrorsSink Recent { get; } = new(10);

    public static void Init(string logDir)
    {
        Directory.CreateDirectory(logDir);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(Path.Combine(logDir, "launcher-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 10,
                fileSizeLimitBytes: 5 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Sink(Recent, LogEventLevel.Warning)
            .CreateLogger();
    }
}

public sealed class RecentErrorsSink(int capacity) : ILogEventSink
{
    private readonly ConcurrentQueue<string> _items = new();

    public void Emit(LogEvent e)
    {
        string line = $"{e.Timestamp:yyyy-MM-dd HH:mm:ss} {e.Level}: {e.RenderMessage()}";
        if (e.Exception is not null) line += $" ({e.Exception.GetType().Name}: {e.Exception.Message})";
        _items.Enqueue(line);
        while (_items.Count > capacity && _items.TryDequeue(out _)) { }
    }

    public IReadOnlyList<string> Snapshot() => _items.ToArray();
}
