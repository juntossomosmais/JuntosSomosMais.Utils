using System.Globalization;

namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>
/// Command-line front end of <see cref="IHistoryPurger"/> (retention): <c>[--collections=a,b] [--from=&lt;date&gt;]
/// [--to=&lt;date&gt;] [--older-than-days=&lt;N&gt;] [--dry-run]</c>. With no arguments it deletes the rows older than
/// <see cref="PurgeHistoryArgs.DefaultOlderThanDays"/> days from every given source.
/// </summary>
public static class PurgeHistoryCommand
{
    private const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>
    /// Parses <paramref name="args"/>, validates the selected collections against <paramref name="sources"/> and only
    /// then creates the purger, so bad input never opens a connection.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="sources">The tracked sources the command may purge.</param>
    /// <param name="createPurger">Creates the provider's purger once the input is valid.</param>
    /// <param name="output">Receives one line per purged history collection and the total.</param>
    /// <param name="cancellationToken">Cancels the purge.</param>
    /// <returns>The process exit code (0).</returns>
    /// <exception cref="ArgumentException">Invalid arguments or an unknown collection.</exception>
    public static Task<int> ExecuteAsync(
        string[] args, IReadOnlyList<HistoryOptions> sources, Func<IHistoryPurger> createPurger, TextWriter output,
        CancellationToken cancellationToken = default)
    {
        var parsed = PurgeHistoryArgs.Parse(args, DateTime.UtcNow);
        SelectSources(sources, parsed.Collections);

        return RunAsync(createPurger(), sources, parsed, output, cancellationToken);
    }

    /// <summary>Purges the selected sources with already parsed arguments and reports the outcome.</summary>
    /// <param name="purger">The provider's purger.</param>
    /// <param name="sources">The tracked sources the command may purge.</param>
    /// <param name="args">The parsed arguments.</param>
    /// <param name="output">Receives one line per purged history collection and the total.</param>
    /// <param name="cancellationToken">Cancels the purge.</param>
    /// <returns>The process exit code (0).</returns>
    /// <exception cref="ArgumentException">An unknown collection.</exception>
    public static async Task<int> RunAsync(
        IHistoryPurger purger, IReadOnlyList<HistoryOptions> sources, PurgeHistoryArgs args, TextWriter output,
        CancellationToken cancellationToken = default)
    {
        var selected = SelectSources(sources, args.Collections);
        var request = new HistoryPurgeRequest(args.FromUtc, args.ToUtc, args.DryRun);

        var results = await purger.PurgeAsync(selected, request, cancellationToken);

        long total = 0;
        foreach (var result in results)
        {
            // The purger reports the bound it applied: an open "--from" interval is pinned to the start of the purge.
            var interval = Describe(args.FromUtc, result.ToUtc ?? args.ToUtc);
            if (result.DryRun)
            {
                total += result.Matched;
                await output.WriteLineAsync($"{result.HistoryName}: {result.Matched} rows {interval} would be deleted (dry run)");
            }
            else
            {
                total += result.Deleted;
                await output.WriteLineAsync($"{result.HistoryName}: deleted {result.Deleted} rows {interval}");
            }
        }

        await output.WriteLineAsync($"Total: {total}");
        return 0;
    }

    /// <summary>All sources when no name is given; otherwise the sources whose history or source collection matches (case-insensitive).</summary>
    private static List<HistoryOptions> SelectSources(IReadOnlyList<HistoryOptions> sources, IReadOnlyList<string> names)
    {
        if (names.Count == 0)
            return sources.ToList();

        var requested = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        var unknown = requested
            .Where(n => !sources.Any(s => Matches(s, n)))
            .ToList();
        if (unknown.Count > 0)
        {
            var valid = sources.SelectMany(s => new[] { s.HistoryName, s.SourceName }).Distinct();
            throw new ArgumentException(
                $"Unknown collection(s): {string.Join(", ", unknown)}. Valid names: {string.Join(", ", valid)}.");
        }

        return sources.Where(s => requested.Any(n => Matches(s, n))).ToList();
    }

    private static bool Matches(HistoryOptions source, string name) =>
        string.Equals(source.HistoryName, name, StringComparison.OrdinalIgnoreCase)
        || string.Equals(source.SourceName, name, StringComparison.OrdinalIgnoreCase);

    private static string Describe(DateTime? fromUtc, DateTime? toUtc)
    {
        var from = fromUtc?.ToString(DateFormat, CultureInfo.InvariantCulture);
        var to = toUtc?.ToString(DateFormat, CultureInfo.InvariantCulture);
        return (from, to) switch
        {
            (null, not null) => $"older than {to}",
            (not null, null) => $"from {from} on",
            _ => $"from {from} to {to}",
        };
    }
}
