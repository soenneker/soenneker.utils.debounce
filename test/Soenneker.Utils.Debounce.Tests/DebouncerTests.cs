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
    private static Task Pause(int ms) => Task.Delay(ms + 25);

    /* --------- TASK overload --------- */

    [Test]
    public async Task Executes_once_after_delay(CancellationToken cancellationToken)
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

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1), System.Threading.CancellationToken.None);
        sw.Stop();

        sw.ElapsedMilliseconds
          .Should().BeInRange(90, 250);        // ~100 ms � jitter
    }

    [Test]
    public async Task Rapid_calls_collapse_to_single_execution(CancellationToken cancellationToken)
    {
        await using var d = new Debouncer();

        var hitCount = 0;
        void Enqueue() =>
            d.Debounce(100, _ =>
            {
                Interlocked.Increment(ref hitCount);
                return Task.CompletedTask;
            }, cancellationToken: cancellationToken);

        Enqueue(); await Task.Delay(20, System.Threading.CancellationToken.None);
        Enqueue(); await Task.Delay(20, System.Threading.CancellationToken.None);
        Enqueue();

        await Pause(150);

        hitCount.Should().Be(1);
    }

    [Test]
    public async Task Sync_Rapid_calls_collapse_to_single_execution(CancellationToken cancellationToken)
    {
        await using var d = new Debouncer();

        var hitCount = 0;
        void Enqueue() =>
            d.Debounce(100, () =>
            {
                Interlocked.Increment(ref hitCount);
            }, cancellationToken: cancellationToken);

        Enqueue(); await Task.Delay(20, System.Threading.CancellationToken.None);
        Enqueue(); await Task.Delay(20, System.Threading.CancellationToken.None);
        Enqueue();

        await Pause(150);

        hitCount.Should().Be(1);
    }

    [Test]
    public async Task RunLeading_invokes_immediately_and_again_after_delay(CancellationToken cancellationToken)
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

        await Pause(125);   // trailing edge

        hitCount.Should().Be(2);
    }

    /* --------- cancellation & disposal --------- */

    [Test]
    public async Task DisposeAsync_cancels_and_awaits_inflight_work(CancellationToken cancellationToken)
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
    public async Task DisposeAsync_waits_for_overlapping_leading_and_trailing_work(CancellationToken cancellationToken)
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

        await trailingFinished.Task.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        Task disposeTask = d.DisposeAsync().AsTask();
        await Task.Delay(25, CancellationToken.None);
        disposeTask.IsCompleted.Should().BeFalse();

        releaseLeading.SetResult();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        leadingFinished.Should().BeTrue();
        invocationCount.Should().Be(2);
    }

    [Test]
    public async Task Canceled_token_prevents_execution()
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
        await Pause(75);

        ran.Should().BeFalse();
    }
}


