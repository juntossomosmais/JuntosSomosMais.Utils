namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>Values of the <c>HistoryType</c> field of a history row.</summary>
public static class HistoryTypes
{
    /// <summary>The document was created: <c>Data</c> holds the full document without <c>_id</c>.</summary>
    public const string Created = "+";

    /// <summary>
    /// The document changed: <c>Data</c> holds the changed top-level fields with their new values, or the whole
    /// document when the row carries <c>ChangedFields</c> (full-document mode).
    /// </summary>
    public const string Changed = "~";

    /// <summary>Full snapshot used when the previous version is unknown (replace without a pre-image).</summary>
    public const string Snapshot = "=";

    /// <summary>The document was deleted: <c>Data</c> holds the last known version when available.</summary>
    public const string Deleted = "-";
}
