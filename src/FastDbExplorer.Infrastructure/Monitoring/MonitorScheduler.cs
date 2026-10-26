using System.Collections.Concurrent;
using FastDbExplorer.Application.Abstractions;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Infrastructure.Monitoring;

public sealed record MonitorRunOutcome(MonitorDefinition Monitor, MonitorCheckResult? Result, Exception? Error);

/// <summary>
/// In-process scheduler. A cheap tick (default 15 s) looks for monitors whose own interval has passed and runs them
/// one at a time (a single query in flight keeps the load on the server minimal). Events are raised on the
/// synchronization context that created the scheduler (the UI thread in the app).
/// </summary>
public sealed class MonitorScheduler : IDisposable
{
    public static readonly TimeSpan DefaultTick = TimeSpan.FromSeconds(15);

    private readonly IMonitorCheckService _checks;
    private readonly ConnectionSettings _settings;
    private readonly Func<IReadOnlyList<MonitorDefinition>> _monitors;
    private readonly TimeProvider _time;
    private readonly TimeSpan _tick;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastAttempt = new();

    public MonitorScheduler(
        IMonitorCheckService checks, ConnectionSettings settings, Func<IReadOnlyList<MonitorDefinition>> monitors,
        TimeProvider? time = null, TimeSpan? tick = null)
    {
        _checks = checks;
        _settings = settings;
        _monitors = monitors;
        _time = time ?? TimeProvider.System;
        _tick = tick ?? DefaultTick;
    }

    public event Action<Guid>? Started;
    public event Action<MonitorRunOutcome>? Completed;

    public void Start() => _ = LoopAsync(_cts.Token);

    /// <summary>Runs one monitor now (manual "check now"), even if it is paused.</summary>
    public async Task CheckNowAsync(Guid id)
    {
        var monitor = _monitors().FirstOrDefault(m => m.Id == id);
        if (monitor is null) return;
        try { await RunOneAsync(monitor, _cts.Token); }
        catch (OperationCanceledException) { }
    }

    /// <summary>A monitor is due when it is enabled and its interval has passed since the last attempt or the last saved check.</summary>
    public static bool IsDue(MonitorDefinition monitor, DateTimeOffset now, DateTimeOffset? lastAttempt)
    {
        if (!monitor.Enabled) return false;

        var last = lastAttempt;
        if (monitor.LastCheckedUtc is { } checkedAt)
        {
            var saved = new DateTimeOffset(DateTime.SpecifyKind(checkedAt, DateTimeKind.Utc));
            if (last is null || saved > last) last = saved;
        }

        var minutes = Math.Clamp(monitor.IntervalMinutes, MonitorDefinition.MinIntervalMinutes, MonitorDefinition.MaxIntervalMinutes);
        return last is null || now - last.Value >= TimeSpan.FromMinutes(minutes);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_tick, _time);
        try
        {
            do
            {
                foreach (var monitor in _monitors().ToList())
                {
                    ct.ThrowIfCancellationRequested();
                    _lastAttempt.TryGetValue(monitor.Id, out var attempt);
                    DateTimeOffset? last = attempt == default ? null : attempt;
                    if (IsDue(monitor, _time.GetUtcNow(), last))
                        await RunOneAsync(monitor, ct);
                }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { }
    }

    private async Task RunOneAsync(MonitorDefinition monitor, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _lastAttempt[monitor.Id] = _time.GetUtcNow();
            Raise(() => Started?.Invoke(monitor.Id));

            MonitorRunOutcome outcome;
            try
            {
                var result = await _checks.CheckAsync(_settings, monitor, ct);
                outcome = new MonitorRunOutcome(monitor.Apply(result), result, null);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // A failed check (server down, column dropped, folder not writable...) must not stop the scheduler.
                outcome = new MonitorRunOutcome(monitor, null, ex);
            }
            Raise(() => Completed?.Invoke(outcome));
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Raise(Action action)
    {
        if (_context is null) action();
        else _context.Post(_ => action(), null);
    }

    public void Dispose() => _cts.Cancel();
}
