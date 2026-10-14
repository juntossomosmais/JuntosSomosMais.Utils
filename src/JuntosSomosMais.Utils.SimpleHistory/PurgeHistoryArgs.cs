using System.Globalization;

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
    private const string FromArg = "--from";
    private const string ToArg = "--to";
    private const string OlderThanDaysArg = "--older-than-days";
    private const string DryRunArg = "--dry-run";

    private const string Usage =
        "Expected '--collections=a,b', '--from=<date>', '--to=<date>', '--older-than-days=<N>' or '--dry-run'.";

    /// <param name="args">The arguments after the command name.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <exception cref="ArgumentException">Unknown, duplicate, empty or inconsistent arguments.</exception>
    public static PurgeHistoryArgs Parse(string[] args, DateTime utcNow)
    {
        var parsed = new Accumulator();
        foreach (var arg in args)
            parsed.Add(arg);
        return parsed.Build(utcNow);
    }

    /// <summary>The values seen so far, filled one argument at a time.</summary>
    private sealed class Accumulator
    {
        private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<string> _collections = Array.Empty<string>();
        private DateTime? _from;
        private DateTime? _to;
        private int? _olderThanDays;
        private bool _dryRun;

        /// <summary>Validates one argument (known, not repeated, valued) and records it.</summary>
        public void Add(string arg)
        {
            var (name, value) = Split(arg);
            if (!IsKnown(name, value))
                throw new ArgumentException($"Unrecognized argument: '{arg}'. {Usage}");
            if (!_seen.Add(name))
                throw new ArgumentException($"Duplicate argument: '{name}'.");

            if (name.Equals(DryRunArg, StringComparison.OrdinalIgnoreCase))
            {
                _dryRun = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Argument '{name}' requires a value.");

            Apply(name, value);
        }

        /// <summary>Checks the arguments against each other and applies the default interval.</summary>
        public PurgeHistoryArgs Build(DateTime utcNow)
        {
            if (_olderThanDays.HasValue && (_from.HasValue || _to.HasValue))
                throw new ArgumentException($"'{OlderThanDaysArg}' cannot be combined with '{FromArg}' or '{ToArg}'.");

            var to = _olderThanDays.HasValue || (!_from.HasValue && !_to.HasValue)
                ? SubtractDays(utcNow, _olderThanDays ?? DefaultOlderThanDays)
                : _to;

            return _from.HasValue && to.HasValue && _from.Value >= to.Value
                ? throw new ArgumentException($"'{FromArg}' ({_from.Value:O}) must be before '{ToArg}' ({to.Value:O}).")
                : new PurgeHistoryArgs(_collections, _from, to, _dryRun);
        }

        private void Apply(string name, string value)
        {
            if (name.Equals(CollectionsArg, StringComparison.OrdinalIgnoreCase))
                _collections = ParseCollections(value);
            else if (name.Equals(FromArg, StringComparison.OrdinalIgnoreCase))
                _from = ParseDate(value, FromArg);
            else if (name.Equals(ToArg, StringComparison.OrdinalIgnoreCase))
                _to = ParseDate(value, ToArg);
            else
                _olderThanDays = ParseOlderThanDays(value);
        }

        private static (string Name, string? Value) Split(string arg)
        {
            var index = arg.IndexOf('=');
            return index < 0 ? (arg, null) : (arg[..index], arg[(index + 1)..]);
        }

        private static bool IsKnown(string name, string? value) =>
            name.Equals(DryRunArg, StringComparison.OrdinalIgnoreCase)
                ? value is null
                : value is not null && (name.Equals(CollectionsArg, StringComparison.OrdinalIgnoreCase)
                    || name.Equals(FromArg, StringComparison.OrdinalIgnoreCase)
                    || name.Equals(ToArg, StringComparison.OrdinalIgnoreCase)
                    || name.Equals(OlderThanDaysArg, StringComparison.OrdinalIgnoreCase));

        private static List<string> ParseCollections(string value)
        {
            var names = value.Split(',').Select(n => n.Trim()).ToList();
            return names.Any(n => n.Length == 0)
                ? throw new ArgumentException($"Argument '{CollectionsArg}' has an empty collection name: '{value}'.")
                : names;
        }

        private static DateTime ParseDate(string value, string name)
        {
            return !DateTime.TryParse(
                    value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
                ? throw new ArgumentException($"Invalid date for argument '{name}': '{value}'. Expected format: yyyy-MM-dd or yyyy-MM-ddTHH:mm:ssZ.")
                : DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        private static int ParseOlderThanDays(string value)
        {
            return !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0
                ? throw new ArgumentException($"Invalid value for argument '{OlderThanDaysArg}': '{value}'. Expected a positive integer.")
                : parsed;
        }

        private static DateTime SubtractDays(DateTime utcNow, int days)
        {
            try
            {
                return DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).AddDays(-days);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentException($"Value of '{OlderThanDaysArg}' is too large: '{days}'.");
            }
        }
    }
}
