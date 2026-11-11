using System.Globalization;

namespace JuntosSomosMais.Utils.Shared;

/// <summary>
/// The arguments every purge command takes (<c>--from</c>, <c>--to</c>, <c>--older-than-days</c> and
/// <c>--dry-run</c>), plus any extra valued ones, recorded one at a time. Linked as source into each package that
/// needs it, so every assembly has its own internal copy.
/// </summary>
internal sealed class PurgeArguments
{
    internal const string FromArg = "--from";
    internal const string ToArg = "--to";
    internal const string OlderThanDaysArg = "--older-than-days";
    internal const string DryRunArg = "--dry-run";

    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _usage;
    private readonly Func<string, DateTime?> _parseDate;
    private readonly Dictionary<string, Action<string>> _extraArgs;
    private int? _olderThanDays;

    /// <param name="usage">Appended to the error for an unrecognized argument.</param>
    /// <param name="parseDate">Parses the value of a date argument into UTC, or returns null when it is invalid.</param>
    /// <param name="extraArgs">Other valued arguments the command takes, each with what records its value.</param>
    public PurgeArguments(
        string usage, Func<string, DateTime?> parseDate, IReadOnlyDictionary<string, Action<string>>? extraArgs = null)
    {
        _usage = usage;
        _parseDate = parseDate;
        _extraArgs = (extraArgs ?? new Dictionary<string, Action<string>>())
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Inclusive lower bound given with <c>--from</c>.</summary>
    public DateTime? From { get; private set; }

    /// <summary>Exclusive upper bound given with <c>--to</c>.</summary>
    public DateTime? To { get; private set; }

    /// <summary>Whether <c>--dry-run</c> was given.</summary>
    public bool DryRun { get; private set; }

    /// <summary>Records every argument, in order.</summary>
    /// <exception cref="ArgumentException">An unknown, duplicate, empty or invalid argument.</exception>
    public void AddAll(IEnumerable<string> args)
    {
        foreach (var arg in args)
            Add(arg);
    }

    /// <summary>Validates one argument (known, not repeated, valued) and records it.</summary>
    /// <exception cref="ArgumentException">An unknown, duplicate, empty or invalid argument.</exception>
    public void Add(string arg)
    {
        var (name, value) = Split(arg);
        if (!IsKnown(name, value))
            throw new ArgumentException($"Unrecognized argument: '{arg}'. {_usage}");
        if (!_seen.Add(name))
            throw new ArgumentException($"Duplicate argument: '{name}'.");

        if (name.Equals(DryRunArg, StringComparison.OrdinalIgnoreCase))
        {
            DryRun = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Argument '{name}' requires a value.");

        Apply(name, value);
    }

    /// <summary>
    /// The upper bound of the purge: <c>--to</c> when an explicit bound was given, otherwise the given (or default)
    /// number of days before <paramref name="utcNow"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><c>--older-than-days</c> combined with a bound, or too large.</exception>
    public DateTime? ResolveTo(DateTime utcNow, int defaultOlderThanDays)
    {
        if (_olderThanDays.HasValue && (From.HasValue || To.HasValue))
            throw new ArgumentException($"'{OlderThanDaysArg}' cannot be combined with '{FromArg}' or '{ToArg}'.");

        return _olderThanDays.HasValue || (!From.HasValue && !To.HasValue)
            ? SubtractDays(utcNow, _olderThanDays ?? defaultOlderThanDays)
            : To;
    }

    /// <exception cref="ArgumentException">Both bounds are set and <see cref="From"/> is not before <paramref name="to"/>.</exception>
    public void EnsureFromBeforeTo(DateTime? to)
    {
        if (From.HasValue && to.HasValue && From.Value >= to.Value)
            throw new ArgumentException($"'{FromArg}' ({From.Value:O}) must be before '{ToArg}' ({to.Value:O}).");
    }

    private void Apply(string name, string value)
    {
        if (name.Equals(FromArg, StringComparison.OrdinalIgnoreCase))
            From = ParseDate(value, FromArg);
        else if (name.Equals(ToArg, StringComparison.OrdinalIgnoreCase))
            To = ParseDate(value, ToArg);
        else if (name.Equals(OlderThanDaysArg, StringComparison.OrdinalIgnoreCase))
            _olderThanDays = ParseOlderThanDays(value);
        else
            _extraArgs[name](value);
    }

    private bool IsKnown(string name, string? value) =>
        name.Equals(DryRunArg, StringComparison.OrdinalIgnoreCase)
            ? value is null
            : value is not null && (name.Equals(FromArg, StringComparison.OrdinalIgnoreCase)
                || name.Equals(ToArg, StringComparison.OrdinalIgnoreCase)
                || name.Equals(OlderThanDaysArg, StringComparison.OrdinalIgnoreCase)
                || _extraArgs.ContainsKey(name));

    private static (string Name, string? Value) Split(string arg)
    {
        var index = arg.IndexOf('=');
        return index < 0 ? (arg, null) : (arg[..index], arg[(index + 1)..]);
    }

    private DateTime ParseDate(string value, string name) =>
        _parseDate(value)
        ?? throw new ArgumentException($"Invalid date for argument '{name}': '{value}'. Expected format: yyyy-MM-dd or yyyy-MM-ddTHH:mm:ssZ.");

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
