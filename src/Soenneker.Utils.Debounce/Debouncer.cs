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
    private readonly object _sync = new();

    private Func<CancellationToken, Task>? _pendingAction;
    private CancellationToken _pendingToken;
    private int _runningCount;
    private TaskCompletionSource? _idleCompletion;
    private bool _disposed;

    private static readonly TimerCallback _tickCb = static s => _ = ((Debouncer) s!).Tick().Preserve();

    public Debouncer()
    {
        _timer = new Timer(_tickCb, this, Timeout.Infinite, Timeout.Infinite);
    }

    public void Debounce(int delayMs, Func<CancellationToken, Task> action, bool runLeading = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (delayMs < Timeout.Infinite)
            throw new ArgumentOutOfRangeException(nameof(delayMs));

        bool runNow;

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            bool hadPending = _pendingAction is not null;
            _pendingAction = action;
            _pendingToken = cancellationToken;
            _timer.Change(delayMs, Timeout.Infinite);

            runNow = !hadPending && runLeading && _runningCount == 0;
            if (runNow)
                ReserveExecution();
        }

        if (runNow)
            _ = Execute(action, cancellationToken).Preserve();
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
        Func<CancellationToken, Task>? action;
        CancellationToken token;
        bool run;

        lock (_sync)
        {
            action = _pendingAction;
            token = _pendingToken;
            _pendingAction = null;
            _pendingToken = default;

            run = !_disposed && action is not null && !token.IsCancellationRequested;
            if (run)
                ReserveExecution();
        }

        if (run)
            await Execute(action!, token).NoSync();
    }

    private async ValueTask Execute(Func<CancellationToken, Task> action, CancellationToken outerCt)
    {
        try
        {
            await action(outerCt).NoSync();
        }
        catch (OperationCanceledException) when (outerCt.IsCancellationRequested)
        {
            /* expected – caller cancelled */
        }
        finally
        {
            CompleteExecution();
        }
    }

    private void ReserveExecution()
    {
        if (_runningCount++ == 0)
            _idleCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void CompleteExecution()
    {
        TaskCompletionSource? completion = null;

        lock (_sync)
        {
            if (--_runningCount == 0)
            {
                completion = _idleCompletion;
                _idleCompletion = null;
            }
        }

        completion?.TrySetResult();
    }

    private Task? MarkDisposedAndGetRunningTask()
    {
        lock (_sync)
        {
            _disposed = true;
            _pendingAction = null;
            _pendingToken = default;
            return _idleCompletion?.Task;
        }
    }

    /// <summary>
    /// Releases resources used by the current instance.
    /// </summary>
    public void Dispose()
    {
        Task? runningTask = MarkDisposedAndGetRunningTask();
        _timer.Dispose();

        if (runningTask is not null)
        {
            try
            {
                runningTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                /* ignore – normal on dispose */
            }
        }
    }

    /// <summary>
    /// Asynchronously releases resources used by the current instance.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        Task? runningTask = MarkDisposedAndGetRunningTask();
        await _timer.DisposeAsync().NoSync();

        if (runningTask is not null)
        {
            try
            {
                await runningTask.NoSync();
            }
            catch (OperationCanceledException)
            {
                /* ignore – normal on dispose */
            }
        }
    }
}
