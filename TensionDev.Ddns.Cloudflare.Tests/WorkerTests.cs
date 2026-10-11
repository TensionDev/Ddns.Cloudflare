using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TensionDev.Ddns.Cloudflare.Configuration;

namespace TensionDev.Ddns.Cloudflare.Tests;

public class WorkerTests
{
    [Fact]
    public async Task Runs_immediately_waits_after_completion_and_never_overlaps()
    {
        var clock = new ManualTimeProvider();
        var entered = Channel.CreateUnbounded<int>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var worker = Create(clock, async token =>
        {
            entered.Writer.TryWrite(Interlocked.Increment(ref calls));
            await release.Task.WaitAsync(token);
        });
        await worker.StartAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, await Read(entered.Reader));
        Assert.Equal(0, clock.TimerCount);
        Assert.Equal(1, calls);

        release.SetResult();
        var timer = await Read(clock.Timers.Reader);
        Assert.Equal(TimeSpan.FromMinutes(5), timer.DueTime);
        Assert.Equal(1, calls);
        timer.Fire();
        Assert.Equal(2, await Read(entered.Reader));
        await Stop(worker);
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failure_or_timeout_waits_then_retries_without_logging_secrets(bool timeout)
    {
        var clock = new ManualTimeProvider();
        var calls = 0;
        var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logger = new CapturingLogger<Worker>();
        using var worker = new Worker(Options.Create(new DdnsOptions { CheckInterval = TimeSpan.FromMinutes(5) }),
            new StubReconciler(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw timeout ? new OperationCanceledException("secret-token") : new HttpRequestException("secret-token");
                retried.TrySetResult();
                return Task.CompletedTask;
            }), clock, logger);
        await worker.StartAsync(TestContext.Current.CancellationToken);
        var timer = await Read(clock.Timers.Reader);
        Assert.Equal(1, calls);
        timer.Fire();
        await retried.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Stop(worker);
        Assert.Equal(2, calls);
        Assert.Equal(1, logger.MessageCount("cycle failed"));
        Assert.All(logger.Messages, message => Assert.DoesNotContain("secret-token", message));
    }

    [Fact]
    public async Task Shutdown_cancels_in_flight_work_and_does_not_retry()
    {
        var clock = new ManualTimeProvider();
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var worker = Create(clock, async token =>
        {
            entered.SetResult(token);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
        await worker.StartAsync(TestContext.Current.CancellationToken);
        var token = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Stop(worker);
        Assert.True(token.IsCancellationRequested);
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Equal(0, clock.TimerCount);
    }

    [Fact]
    public async Task Shutdown_interrupts_interval_wait()
    {
        var clock = new ManualTimeProvider();
        using var worker = Create(clock, _ => Task.CompletedTask);
        await worker.StartAsync(TestContext.Current.CancellationToken);
        var timer = await Read(clock.Timers.Reader);
        await Stop(worker);
        Assert.True(timer.Disposed);
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    private static Worker Create(TimeProvider clock, Func<CancellationToken, Task> action) =>
        new(Options.Create(new DdnsOptions { CheckInterval = TimeSpan.FromMinutes(5) }),
            new StubReconciler(action), clock, NullLogger<Worker>.Instance);

    private static async Task<T> Read<T>(ChannelReader<T> reader) =>
        await reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    private static async Task Stop(Worker worker) =>
        await worker.StopAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    // Fire delays explicitly, without wall-clock sleeps.
    private sealed class ManualTimeProvider : TimeProvider
    {
        public Channel<ManualTimer> Timers { get; } = Channel.CreateUnbounded<ManualTimer>();
        private int _timerCount;
        public int TimerCount => Volatile.Read(ref _timerCount);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, dueTime);
            Interlocked.Increment(ref _timerCount);
            Timers.Writer.TryWrite(timer);
            return timer;
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
    {
        public TimeSpan DueTime { get; } = dueTime;
        public bool Disposed { get; private set; }
        public void Fire() { if (!Disposed) callback(state); }
        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
        public void Dispose() => Disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
