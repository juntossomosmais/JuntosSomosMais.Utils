using System.Globalization;

namespace JuntosSomosMais.Utils.RequestAudit;

/// <summary>
/// Command-line front end of <see cref="IRequestAuditPurger"/> (retention): <c>[--from=&lt;date&gt;]
/// [--to=&lt;date&gt;] [--older-than-days=&lt;N&gt;] [--dry-run]</c>. With no arguments it deletes the entries older
/// than <see cref="PurgeRequestAuditArgs.DefaultOlderThanDays"/> days.
/// </summary>
public static class PurgeRequestAuditCommand
{
    private const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>Parses <paramref name="args"/> and only then creates the purger, so bad input never opens a connection.</summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="createPurger">Creates the provider's purger once the input is valid.</param>
    /// <param name="output">Receives the outcome.</param>
    /// <param name="cancellationToken">Cancels the purge.</param>
    /// <returns>The process exit code (0).</returns>
    /// <exception cref="ArgumentException">Invalid arguments.</exception>
    public static Task<int> ExecuteAsync(
        string[] args, Func<IRequestAuditPurger> createPurger, TextWriter output, CancellationToken cancellationToken = default)
    {
        var parsed = PurgeRequestAuditArgs.Parse(args, DateTime.UtcNow);

        return RunAsync(createPurger(), parsed, output, cancellationToken);
    }

    /// <summary>Purges with already parsed arguments and reports the outcome.</summary>
    /// <param name="purger">The provider's purger.</param>
    /// <param name="args">The parsed arguments.</param>
    /// <param name="output">Receives the outcome.</param>
    /// <param name="cancellationToken">Cancels the purge.</param>
    /// <returns>The process exit code (0).</returns>
    public static async Task<int> RunAsync(
        IRequestAuditPurger purger, PurgeRequestAuditArgs args, TextWriter output, CancellationToken cancellationToken = default)
    {
        var result = await purger.PurgeAsync(
            new RequestAuditPurgeRequest(args.FromUtc, args.ToUtc, args.DryRun), cancellationToken);

        // The purger reports the bound it applied: an open "--from" interval is pinned to the start of the purge.
        var interval = Describe(args.FromUtc, result.ToUtc);
        await output.WriteLineAsync(result.DryRun
            ? $"{result.Matched} request audit entries {interval} would be deleted (dry run)"
            : $"Deleted {result.Deleted} request audit entries {interval}");
        return 0;
    }

    private static string Describe(DateTime? fromUtc, DateTime toUtc)
    {
        var to = toUtc.ToString(DateFormat, CultureInfo.InvariantCulture);
        return fromUtc is null
            ? $"older than {to}"
            : $"from {fromUtc.Value.ToString(DateFormat, CultureInfo.InvariantCulture)} to {to}";
    }
}
