using System.Globalization;
using JuntosSomosMais.Utils.Shared;

namespace JuntosSomosMais.Utils.RequestAudit;

/// <summary>Parsed arguments of the purge command (see <see cref="PurgeRequestAuditCommand"/>).</summary>
/// <param name="FromUtc">Inclusive lower bound on the creation date.</param>
/// <param name="ToUtc">Exclusive upper bound on the creation date.</param>
/// <param name="DryRun">Only count, delete nothing.</param>
public sealed record PurgeRequestAuditArgs(DateTime? FromUtc, DateTime? ToUtc, bool DryRun)
{
    /// <summary>Age of the entries deleted when no interval is given.</summary>
    public const int DefaultOlderThanDays = 30;

    private const string Usage = "Expected '--from=<date>', '--to=<date>', '--older-than-days=<N>' or '--dry-run'.";

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ssK"];

    /// <param name="args">The arguments after the command name.</param>
    /// <param name="utcNow">Upper limit of both bounds.</param>
    /// <exception cref="ArgumentException">Unknown, duplicate, empty or inconsistent arguments.</exception>
    public static PurgeRequestAuditArgs Parse(string[] args, DateTime utcNow)
    {
        var parsed = new PurgeArguments(Usage, ParseDate);
        parsed.AddAll(args);

        var to = parsed.ResolveTo(utcNow, DefaultOlderThanDays);
        if (parsed.From >= utcNow)
            throw new ArgumentException(
                $"'{PurgeArguments.FromArg}' ({parsed.From.Value:O}) must be before the current time ({utcNow:O}).");
        if (to > utcNow)
            throw new ArgumentException(
                $"'{PurgeArguments.ToArg}' ({to.Value:O}) cannot be after the current time ({utcNow:O}).");

        parsed.EnsureFromBeforeTo(to);
        return new PurgeRequestAuditArgs(parsed.From, to, parsed.DryRun);
    }

    private static DateTime? ParseDate(string value) =>
        DateTimeOffset.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.UtcDateTime
            : null;
}
