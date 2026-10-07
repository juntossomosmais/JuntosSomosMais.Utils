using MongoDB.Bson;
using MongoDB.Driver;
namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>
/// Pure mapping from a change stream event to a history row (or null when nothing changed). One instance per
/// tracked collection, because the ignored/tracked fields and the row shape are per-collection options.
/// </summary>
public sealed class HistoryMapper
{
    private readonly HashSet<string> _ignoredFields;
    private readonly HashSet<string> _trackedFields;
    private readonly bool _storeFullDocument;

    /// <param name="options">Options of the tracked collection (ignored/tracked fields, row shape).</param>
    public HistoryMapper(HistoryOptions options)
    {
        _ignoredFields = new HashSet<string>(options.IgnoredFields, StringComparer.Ordinal);
        _trackedFields = new HashSet<string>(options.TrackedFields, StringComparer.Ordinal);
        _storeFullDocument = options.StoreFullDocument;
    }

    /// <summary>Maps one event to a history row, or null when it carries nothing worth recording.</summary>
    public HistoryEntry? Map(ChangeEvent change) => change.Operation switch
    {
        ChangeStreamOperationType.Insert when change.After is not null => NewRow(change, HistoryTypes.Created, Filter(change.After)),
        ChangeStreamOperationType.Delete => NewRow(change, HistoryTypes.Deleted, change.Before is null ? new BsonDocument() : Filter(change.Before)),
        ChangeStreamOperationType.Replace or ChangeStreamOperationType.Update => MapModification(change),
        _ => null,
    };

    private HistoryEntry? MapModification(ChangeEvent change)
    {
        if (change.After is null)
            return null;

        if (change.Before is not null)
        {
            var (changed, removed) = Diff(change.Before, change.After);
            return changed.ElementCount == 0 && removed.Count == 0
                ? null
                : ChangedRow(change, change.After, changed, removed);
        }

        return change.Operation == ChangeStreamOperationType.Replace
            ? NewRow(change, HistoryTypes.Snapshot, Filter(change.After))
            : MapUpdateWithoutBefore(change, change.After);
    }

    private HistoryEntry? MapUpdateWithoutBefore(ChangeEvent change, BsonDocument after)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var key in change.UpdatedFields?.Names ?? Enumerable.Empty<string>())
            names.Add(TopLevel(key));
        foreach (var key in change.TruncatedArrayFieldNames ?? Array.Empty<string>())
            names.Add(TopLevel(key));

        var changed = new BsonDocument();
        foreach (var name in names.OrderBy(n => n, StringComparer.Ordinal))
        {
            if (!CountsAsChange(name) || !after.TryGetValue(name, out var value))
                continue;
            changed[name] = value;
        }

        var removed = (change.RemovedFieldNames ?? Array.Empty<string>())
            .Select(TopLevel)
            .Where(n => CountsAsChange(n) && !after.Contains(n))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        return changed.ElementCount == 0 && removed.Count == 0
            ? null
            : ChangedRow(change, after, changed, removed);
    }

    /// <summary>Changed fields (new values) and removed fields between two versions, honouring the options.</summary>
    internal (BsonDocument Changed, List<string> Removed) Diff(BsonDocument before, BsonDocument after)
    {
        var changed = new BsonDocument();
        foreach (var element in after)
        {
            if (!CountsAsChange(element.Name))
                continue;
            if (!before.TryGetValue(element.Name, out var previous) || previous != element.Value)
                changed[element.Name] = element.Value;
        }

        var removed = before.Names
            .Where(n => CountsAsChange(n) && !after.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        return (changed, removed);
    }

    // Delta mode: the changed fields only. Full-document mode: the whole (filtered) document plus the changed names,
    // whose presence tells a reader that Data is not a delta.
    private HistoryEntry ChangedRow(ChangeEvent change, BsonDocument after, BsonDocument changed, List<string> removed) =>
        _storeFullDocument
            ? NewRow(change, HistoryTypes.Changed, Filter(after), removed, changed.Names.OrderBy(n => n, StringComparer.Ordinal).ToList())
            : NewRow(change, HistoryTypes.Changed, changed, removed);

    private bool IsStored(string name) =>
        name != "_id" && (_trackedFields.Count == 0 || _trackedFields.Contains(name));

    private bool CountsAsChange(string name) => IsStored(name) && !_ignoredFields.Contains(name);

    private static string TopLevel(string path)
    {
        var dot = path.IndexOf('.');
        return dot < 0 ? path : path[..dot];
    }

    private BsonDocument Filter(BsonDocument document)
    {
        var copy = document.DeepClone().AsBsonDocument;
        foreach (var name in copy.Names.Where(n => !IsStored(n)).ToList())
            copy.Remove(name);
        return copy;
    }

    private static HistoryEntry NewRow(
        ChangeEvent change, string type, BsonDocument data, List<string>? removed = null, List<string>? changedFields = null) => new()
        {
            EntityId = change.EntityId,
            HistoryDate = change.TimestampUtc,
            HistoryType = type,
            Data = data,
            RemovedFields = removed is { Count: > 0 } ? removed : null,
            ChangedFields = changedFields,
            ChangeToken = change.Token,
        };
}
