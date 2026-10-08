using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>
/// Runs the setup and the change stream consumer of ONE tracked collection with retry and exponential backoff
/// (taken from <see cref="HistoryOptions"/>). One instance per tracked collection, so a failing source never blocks
/// another.
/// </summary>
public class HistoryHostedService : BackgroundService
{
    private readonly IHistorySetup _setup;
    private readonly IHistoryProcessor _processor;
    private readonly ILogger _logger;
    private readonly HistoryOptions _options;
    private readonly HistoryHealthState _state;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <param name="setup">Prepares the collections of the tracked collection.</param>
    /// <param name="processor">Consumes its change stream.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="options">Options of the tracked collection (backoff, names).</param>
    /// <param name="state">Health state of the tracked collection; failures are recorded there.</param>
    public HistoryHostedService(
        IHistorySetup setup, IHistoryProcessor processor, ILogger logger, HistoryOptions options, HistoryHealthState state)
        : this(setup, processor, logger, options, state, Task.Delay)
    {
    }

    // Lets the tests observe the requested delays instead of waiting for them.
    internal HistoryHostedService(
        IHistorySetup setup,
        IHistoryProcessor processor,
        ILogger logger,
        HistoryOptions options,
        HistoryHealthState state,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _setup = setup;
        _processor = processor;
        _logger = logger;
        _options = options;
        _state = state;
        _delay = delay;
    }

    /// <summary>Doubles <paramref name="current"/> up to <paramref name="max"/>.</summary>
    public static TimeSpan NextBackoff(TimeSpan current, TimeSpan max)
    {
        var doubled = TimeSpan.FromTicks(current.Ticks * 2);
        return doubled > max ? max : doubled;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backoff = _options.InitialBackoff;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Runs the setup again: a recreated source needs its pre/post-images back.
                await _setup.EnsureSetupAsync(stoppingToken);
                await _processor.RunAsync(stoppingToken);

                // A clean return (stream closed or gap) is not a failure.
                backoff = _options.InitialBackoff;
            }
            catch (Exception) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down: a cancellation or a socket closed under the driver is not worth an error log.
                break;
            }
            catch (Exception ex)
            {
                _state.MarkFailure(ex);
                _logger.LogError(
                    ex, "{SourceName} history stream failed, retrying in {RetryDelaySeconds:0.##}s", _options.SourceName, backoff.TotalSeconds);
                try
                {
                    await _delay(backoff, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                backoff = NextBackoff(backoff, _options.MaxBackoff);
                continue;
            }

            try
            {
                await _delay(_options.InitialBackoff, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
