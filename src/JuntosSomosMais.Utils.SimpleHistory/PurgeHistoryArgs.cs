using System.Globalization;
using JuntosSomosMais.Utils.Shared;

namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>Parsed arguments of the purge command (see <see cref="PurgeHistoryCommand"/>).</summary>
/// <param name="Collections">Selected collection names (history or source); empty means all.</param>
/// <param name="FromUtc">Inclusive lower bound on the history date.</param>
/// <param name="ToUtc">Exclusive upper bound on the history date.</param>
/// <param name="DryRun">Only count, delete nothing.</param>
public sealed record PurgeHistoryArgs(IReadOnlyList<string> Collections, DateTime? FromUtc, DateTime? ToUtc, bool DryRun)
{
    /// <summary>Age of the rows deleted when no interval is given.</summary>
    public const int DefaultOlderThanDays = 365;

    private const string CollectionsArg = "--collections";

    private const string Usage =
        "Expected '--collections=a,b', '--from=<date>', '--to=<date>', '--older-than-days=<N>' or '--dry-run'.";

    /// <param name="args">The arguments after the command name.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <exception cref="ArgumentException">Unknown, duplicate, empty or inconsistent arguments.</exception>
    public static PurgeHistoryArgs Parse(string[] args, DateTime utcNow)
    {
        IReadOnlyList<string> collections = Array.Empty<string>();
        var parsed = new PurgeArguments(
            Usage,
            ParseDate,
            new Dictionary<string, Action<string>> { [CollectionsArg] = value => collections = ParseCollections(value) });
        parsed.AddAll(args);

        var to = parsed.ResolveTo(utcNow, DefaultOlderThanDays);
        parsed.EnsureFromBeforeTo(to);
        return new PurgeHistoryArgs(collections, parsed.From, to, parsed.DryRun);
    }

    private static List<string> ParseCollections(string value)
    {
        var names = value.Split(',').Select(n => n.Trim()).ToList();
        return names.Any(n => n.Length == 0)
            ? throw new ArgumentException($"Argument '{CollectionsArg}' has an empty collection name: '{value}'.")
            : names;
    }

    private static DateTime? ParseDate(string value) =>
        DateTime.TryParse(
            value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : null;
}
