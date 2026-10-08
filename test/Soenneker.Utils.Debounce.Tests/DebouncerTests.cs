using AwesomeAssertions;
using Soenneker.Tests.HostedUnit;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Utils.Debounce.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class DebouncerTests : HostedUnitTest
{

    public DebouncerTests(Host host) : base(host)
    {
    }

    [Test]
    public void Default()
    {

    }


    // Small jitter cushion so that CI boxes don�t fail on tight timing assertions
    private static Task Pause(int ms, CancellationToken cancellationToken = default) => Task.Delay(ms + 25, cancellationToken: cancellationToken);

    /* --------- TASK overload --------- */

    [Test]
    public async ValueTask Executes_once_after_delay(CancellationToken cancellationToken)
    {
        await using var d = new Debouncer();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sw = Stopwatch.StartNew();

        d.Debounce(
            delayMs: 100,
            action: _ =>
            {
                tcs.SetResult();
                return Task.CompletedTask;
            }, cancellationToken: cancellationToken);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
        sw.Stop();

        sw.ElapsedMilliseconds
          .Should().BeInRange(90, 250);        // ~100 ms � jitter
    }

    [Test]
    public async ValueTask Rapid_calls_collapse_to_single_execution(CancellationToken cancellationToken)
    {
        await using var d = new Debouncer();

        var hitCount = 0;
        void Enqueue() =>
            d.Debounce(100, _ =>
            {
                Interlocked.Increment(ref hitCount);
                return Task.CompletedTask;
            }, cancellationToken: cancellationToken);

        Enqueue(); await Task.Delay(20, cancellationToken);
        Enqueue(); await Task.Delay(20, cancellationToken);
        Enqueue();

        await Pause(150, cancellationToken: cancellationToken);

        hitCount.Should().Be(1);
    }

    [Test]
    public async ValueTask Sync_Rapid_calls_collapse_to_single_execution(CancellationToken cancellationToken)
    {
        await using var d = new Debouncer();

        var hitCount = 0;
        void Enqueue() =>
            d.Debounce(100, () =>
            {
                Interlocked.Increment(ref hitCount);
            }, cancellationToken: cancellationToken);

        Enqueue(); await Task.Delay(20, cancellationToken);
        Enqueue(); await Task.Delay(20, cancellationToken);
        Enqueue();

        await Pause(150, cancellationToken: cancellationToken);

        hitCount.Should().Be(1);
    }

    [Test]
    public async ValueTask RunLeading_invokes_immediately_and_again_after_delay(CancellationToken cancellationToken)
    {
        await using var d = new Debouncer();

        var hitCount = 0;

        d.Debounce(
            delayMs: 100,
            action: _ =>
            {
                Interlocked.Increment(ref hitCount);
                return Task.CompletedTask;
            },
            runLeading: true, cancellationToken);

        // leading edge
        hitCount.Should().Be(1);

        await Pause(125, cancellationToken: cancellationToken);   // trailing edge

        hitCount.Should().Be(2);
    }

    /* --------- cancellation & disposal --------- */

    [Test]
    public async ValueTask DisposeAsync_cancels_and_awaits_inflight_work(CancellationToken cancellationToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = false;

        await using (var d = new Debouncer())
        {
            d.Debounce(10, async ct =>
            {
                started.SetResult();
                await Task.Delay(150, ct);
                finished = true;
            }, cancellationToken: cancellationToken);

            await started.Task;          // ensure the delegate actually began
        }                                 // DisposeAsync should block here

        finished.Should().BeTrue();       // proves DisposeAsync awaited the task
    }

    [Test]
    public async ValueTask DisposeAsync_waits_for_overlapping_leading_and_trailing_work(CancellationToken cancellationToken)
    {
        var releaseLeading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var trailingFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var leadingFinished = false;
        var invocationCount = 0;
        var d = new Debouncer();

        d.Debounce(25, async _ =>
        {
            if (Interlocked.Increment(ref invocationCount) == 1)
            {
                await releaseLeading.Task;
                leadingFinished = true;
                return;
            }

            trailingFinished.SetResult();
        }, runLeading: true, cancellationToken: cancellationToken);

        await trailingFinished.Task.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);

        Task disposeTask = d.DisposeAsync().AsTask();
        await Task.Delay(25, cancellationToken);
        disposeTask.IsCompleted.Should().BeFalse();

        releaseLeading.SetResult();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);

        leadingFinished.Should().BeTrue();
        invocationCount.Should().Be(2);
    }

    [Test]
    public async ValueTask Canceled_token_prevents_execution(CancellationToken cancellationToken)
    {
        await using var d = new Debouncer();

        using var cts = new CancellationTokenSource();
        var ran = false;

        d.Debounce(50, _ =>
        {
            ran = true;
            return Task.CompletedTask;
        }, cancellationToken: cts.Token);

        await cts.CancelAsync();          // cancel before the delay elapses
        await Pause(75, cancellationToken: cancellationToken);

        ran.Should().BeFalse();
    }
}


