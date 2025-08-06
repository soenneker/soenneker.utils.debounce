using System;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Debounce.Abstract;

namespace Soenneker.Utils.Debounce;

/// <inheritdoc cref="IDebouncer"/>
public sealed class Debouncer : IDebouncer
{
    private readonly Timer _timer;

    private TaskWork? _pendingTask;
    private Task? _runningTask;

    private static readonly TimerCallback _tickCb = static s => _ = ((Debouncer) s!).Tick().Preserve();

    public Debouncer()
    {
        _timer = new Timer(_tickCb, this, Timeout.Infinite, Timeout.Infinite);
    }

    public void Debounce(int delayMs, Func<CancellationToken, Task> action, bool runLeading = false, CancellationToken cancellationToken = default)
    {
        TaskWork? previous = Interlocked.Exchange(ref _pendingTask, new TaskWork(action, cancellationToken));

        if (previous is null)
        {
            if (runLeading && _runningTask is null)
                _ = Execute(action, cancellationToken); // fire leading

            _timer.Change(delayMs, Timeout.Infinite);
        }
        else
        {
            // Reset the timer for subsequent calls
            _timer.Change(delayMs, Timeout.Infinite);
        }
    }

    public void Debounce(int delayMs, Action<CancellationToken> action, bool runLeading = false, CancellationToken cancellationToken = default)
    {
        Debounce(delayMs, ct =>
        {
            action(ct); // run synchronously
            return Task.CompletedTask;
        }, runLeading, cancellationToken);
    }

    public void Debounce(int delayMs, Action action, bool runLeading = false, CancellationToken cancellationToken = default)
    {
        Debounce(delayMs, _ =>
        {
            action(); // run synchronously
            return Task.CompletedTask;
        }, runLeading, cancellationToken);
    }

    private async ValueTask Tick()
    {
        TaskWork? taskToRun = Interlocked.Exchange(ref _pendingTask, null);

        if (taskToRun is not null && !taskToRun.Token.IsCancellationRequested)
            await Execute(taskToRun.Action, taskToRun.Token).NoSync();
    }

    private async Task Execute(Func<CancellationToken, Task> action, CancellationToken outerCt)
    {
        _runningTask = action(outerCt); // ⬅ no linked token

        try
        {
            await _runningTask.NoSync();
        }
        catch (OperationCanceledException) when (outerCt.IsCancellationRequested)
        {
            /* expected – caller cancelled */
        }
        finally
        {
            _runningTask = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _timer.DisposeAsync().NoSync();

        if (_runningTask is { } t)
        {
            try
            {
                await t.NoSync();
            }
            catch (OperationCanceledException)
            {
                /* ignore – normal on dispose */
            }
        }
    }

    private sealed record TaskWork(Func<CancellationToken, Task> Action, CancellationToken Token);
}