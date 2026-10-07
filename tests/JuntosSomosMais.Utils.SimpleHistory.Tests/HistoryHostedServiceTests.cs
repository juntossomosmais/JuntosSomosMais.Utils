using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace JuntosSomosMais.Utils.SimpleHistory.Tests;

// The service loop runs on a background thread while the test thread polls, so every shared counter is a
// ConcurrentQueue or an Interlocked int.
public class HistoryHostedServiceTests
{
    private static readonly TimeSpan FastBackoff = TimeSpan.FromMilliseconds(10);

    private readonly Mock<IHistorySetup> _setup = new();
    private readonly Mock<IHistoryProcessor> _processor = new();
    private readonly Mock<ILogger> _logger = new();
    private static readonly DateTime Now = new(2026, 10, 7, 9, 0, 0, DateTimeKind.Utc);

    private readonly HistoryHealthState _state = new(new FakeTimeProvider(Now));

    private HistoryHostedService CreateSut() => CreateSut(FastBackoff, TimeSpan.FromMilliseconds(40));

    private HistoryHostedService CreateSut(TimeSpan initialBackoff, TimeSpan maxBackoff) =>
        new(_setup.Object, _processor.Object, _logger.Object,
            new HistoryOptions("myOrders") { InitialBackoff = initialBackoff, MaxBackoff = maxBackoff }, _state);

    /// <summary>
    /// A service whose delays complete at once and are recorded, so the backoff sequence is observable and
    /// deterministic. The service is stopped by cancelling its token from inside the processor once the scenario ran.
    /// </summary>
    private HistoryHostedService CreateSutRecordingDelays(ConcurrentQueue<TimeSpan> delays, TimeSpan initialBackoff, TimeSpan maxBackoff) =>
        new(_setup.Object, _processor.Object, _logger.Object,
            new HistoryOptions("myOrders") { InitialBackoff = initialBackoff, MaxBackoff = maxBackoff }, _state,
            (delay, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                delays.Enqueue(delay);
                return Task.CompletedTask;
            });

    private void VerifyErrorLogged(string fragment, Times times) =>
        _logger.Verify(l => l.Log(
            LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(fragment)),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    private void VerifyErrorLoggedWithException(string message, Times times) =>
        _logger.Verify(l => l.Log(
            LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.Is<Exception?>(e => e != null && e.Message == message),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    private void VerifyNoErrorLogged() =>
        _logger.Verify(l => l.Log(
            LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Condition not reached in time.");
            await Task.Delay(5);
        }
    }

    // Records "setup" and "run" in order; the processor outcome of run N (1-based) comes from outcome(N).
    private ConcurrentQueue<string> RecordCalls(Func<int, Task> outcome)
    {
        var calls = new ConcurrentQueue<string>();
        var runs = 0;
        _setup.Setup(s => s.EnsureSetupAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Enqueue("setup")).Returns(Task.CompletedTask);
        _processor.Setup(p => p.RunAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls.Enqueue("run");
                return outcome(Interlocked.Increment(ref runs));
            });
        return calls;
    }

    private static int Count(ConcurrentQueue<string> calls, string name) => calls.Count(c => c == name);

    [Fact]
    public async Task ExecuteAsync_ShouldRunSetupBeforeProcessor()
    {
        var calls = RecordCalls(_ => Task.CompletedTask);
        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => calls.Contains("run"));
        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(new[] { "setup", "run" }, calls.Take(2));
    }

    [Fact]
    public async Task ExecuteAsync_WhenProcessorReturns_ShouldRetryAfterBackoff()
    {
        var calls = RecordCalls(_ => Task.CompletedTask);
        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Count(calls, "run") >= 3);
        await sut.StopAsync(CancellationToken.None);

        Assert.True(Count(calls, "run") >= 3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProcessorReturnsCleanly_ShouldRunSetupAgainBeforeNextRun()
    {
        using var cts = new CancellationTokenSource();
        var calls = RecordCalls(run =>
        {
            if (run == 3)
                cts.Cancel();
            return Task.CompletedTask;
        });
        var sut = CreateSutRecordingDelays(new ConcurrentQueue<TimeSpan>(), FastBackoff, TimeSpan.FromMilliseconds(40));

        await sut.StartAsync(cts.Token);
        await sut.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { "setup", "run", "setup", "run", "setup", "run" }, calls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProcessorThrows_ShouldLogAndRunAgain()
    {
        var calls = RecordCalls(run => run == 1 ? Task.FromException(new InvalidOperationException("boom")) : Task.CompletedTask);
        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Count(calls, "run") >= 2);
        await sut.StopAsync(CancellationToken.None);

        VerifyErrorLogged("myOrders history stream failed", Times.Once());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProcessorThrows_ShouldLogWithTheException()
    {
        var calls = RecordCalls(run => run == 1 ? Task.FromException(new InvalidOperationException("boom")) : Task.CompletedTask);
        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Count(calls, "run") >= 2);
        await sut.StopAsync(CancellationToken.None);

        VerifyErrorLoggedWithException("boom", Times.Once());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProcessorThrows_ShouldRunSetupAgainBeforeNextRun()
    {
        var calls = RecordCalls(run => run == 1 ? Task.FromException(new InvalidOperationException("boom")) : Task.CompletedTask);
        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Count(calls, "run") >= 2);
        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(new[] { "setup", "run", "setup", "run" }, calls.Take(4));
    }

    [Fact]
    public async Task ExecuteAsync_Backoff_ShouldDoubleOnFailuresAndResetAfterSuccess()
    {
        // fail, fail, succeed, fail: 10 (first failure), 20 (doubled), 10 (idle wait after the clean run, which also
        // resets the backoff), 10 (first failure again instead of 40).
        using var cts = new CancellationTokenSource();
        RecordCalls(run => run switch
        {
            1 or 2 or 4 => Task.FromException(new InvalidOperationException(run.ToString())),
            3 => Task.CompletedTask,
            _ => CancelAndComplete(cts),
        });
        var delays = new ConcurrentQueue<TimeSpan>();
        var sut = CreateSutRecordingDelays(delays, FastBackoff, TimeSpan.FromMilliseconds(100));

        await sut.StartAsync(cts.Token);
        await sut.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { 10, 20, 10, 10 }, delays.Select(d => (int)d.TotalMilliseconds));
    }

    [Fact]
    public async Task ExecuteAsync_Backoff_ShouldBeCappedAtMaxBackoff()
    {
        using var cts = new CancellationTokenSource();
        RecordCalls(run =>
        {
            if (run > 4)
                cts.Cancel();
            return Task.FromException(new InvalidOperationException("boom"));
        });
        var delays = new ConcurrentQueue<TimeSpan>();
        var sut = CreateSutRecordingDelays(delays, FastBackoff, TimeSpan.FromMilliseconds(25));

        await sut.StartAsync(cts.Token);
        await sut.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { 10, 20, 25, 25 }, delays.Select(d => (int)d.TotalMilliseconds));
    }

    [Fact]
    public async Task ExecuteAsync_WhenProcessorThrows_ShouldMarkTheFailureWithTheStatesClock()
    {
        // Arrange
        var calls = RecordCalls(run => run == 1 ? Task.FromException(new InvalidOperationException("boom")) : Task.CompletedTask);
        var sut = CreateSut();

        // Act
        await sut.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Count(calls, "run") >= 2);
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal("InvalidOperationException", _state.LastError);
        Assert.Equal(Now, _state.LastErrorUtc);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSetupFails_ShouldLogRetrySetupAndNotRunProcessorUntilItSucceeds()
    {
        var setupAttempts = 0;
        _setup.Setup(s => s.EnsureSetupAsync(It.IsAny<CancellationToken>()))
            .Returns(() => Interlocked.Increment(ref setupAttempts) == 1
                ? Task.FromException(new InvalidOperationException("no collMod privilege"))
                : Task.CompletedTask);
        var processorRuns = 0;
        _processor.Setup(p => p.RunAsync(It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref processorRuns)).Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Volatile.Read(ref processorRuns) >= 1);
        await sut.StopAsync(CancellationToken.None);

        Assert.True(Volatile.Read(ref setupAttempts) >= 2);
        VerifyErrorLoggedWithException("no collMod privilege", Times.Once());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledWhileProcessing_ShouldExitPromptlyWithoutLoggingError()
    {
        var started = new TaskCompletionSource();
        _processor.Setup(p => p.RunAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
            });
        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(sut.ExecuteTask!.IsCompleted);
        VerifyNoErrorLogged();
    }

    [Fact]
    public async Task ExecuteAsync_WhenProcessorFailsWithANonCancellationErrorDuringShutdown_ShouldExitWithoutLoggingError()
    {
        // The driver can surface a closed socket (not an OperationCanceledException) when the host stops.
        var started = new TaskCompletionSource();
        _processor.Setup(p => p.RunAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    throw new IOException("socket closed");
                }
            });
        var sut = CreateSut();

        await sut.StartAsync(CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(sut.ExecuteTask!.IsCompletedSuccessfully);
        VerifyNoErrorLogged();
        Assert.Null(_state.LastError);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledDuringBackoff_ShouldExitPromptly()
    {
        var sut = CreateSut(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        var ran = new TaskCompletionSource();
        _processor.Setup(p => p.RunAsync(It.IsAny<CancellationToken>()))
            .Callback(() => ran.TrySetResult()).Returns(Task.CompletedTask);

        await sut.StartAsync(CancellationToken.None);
        await ran.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(sut.ExecuteTask!.IsCompleted); // did not wait the 5 min backoff
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledDuringFailureBackoff_ShouldExitPromptly()
    {
        var sut = CreateSut(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        var failed = new TaskCompletionSource();
        _processor.Setup(p => p.RunAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                failed.TrySetResult();
                return Task.FromException(new InvalidOperationException("boom"));
            });

        await sut.StartAsync(CancellationToken.None);
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(sut.ExecuteTask!.IsCompleted); // did not wait the 5 min failure backoff
    }

    [Theory]
    [InlineData(5, 60, 10)]
    [InlineData(40, 60, 60)]
    [InlineData(60, 60, 60)]
    public void NextBackoff_ShouldDoubleAndCapAtMaximum(int currentSeconds, int maxSeconds, int expectedSeconds)
    {
        var next = HistoryHostedService.NextBackoff(
            TimeSpan.FromSeconds(currentSeconds), TimeSpan.FromSeconds(maxSeconds));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), next);
    }

    private static Task CancelAndComplete(CancellationTokenSource cts)
    {
        cts.Cancel();
        return Task.CompletedTask;
    }
}
