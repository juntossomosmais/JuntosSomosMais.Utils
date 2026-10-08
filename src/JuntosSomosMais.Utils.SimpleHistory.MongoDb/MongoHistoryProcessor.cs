using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>Change stream consumer of one tracked collection. Works with any BSON type of <c>_id</c>.</summary>
public class MongoHistoryProcessor : IHistoryProcessor
{
    private const int ChangeStreamHistoryLostCode = 286;

    private readonly IMongoCollection<BsonDocument> _source;
    private readonly IMongoCollection<HistoryEntry> _history;
    private readonly MongoHistoryOptions _options;
    private readonly HistoryMapper _mapper;
    private readonly IHistoryCheckpointStore _checkpoints;
    private readonly HistoryHealthState _state;
    private readonly ILogger _logger;

    /// <param name="database">Database holding the source, history and checkpoint collections.</param>
    /// <param name="options">Options of the tracked collection.</param>
    /// <param name="checkpoints">Resume token of this tracked collection.</param>
    /// <param name="state">Health state of this tracked collection; every saved checkpoint is recorded there.</param>
    /// <param name="logger">Logger.</param>
    public MongoHistoryProcessor(
        IMongoDatabase database,
        MongoHistoryOptions options,
        IHistoryCheckpointStore checkpoints,
        HistoryHealthState state,
        ILogger logger)
        : this(
            database.GetCollection<BsonDocument>(options.SourceName),
            database.GetCollection<HistoryEntry>(options.HistoryName),
            options,
            checkpoints,
            state,
            logger)
    {
    }

    internal MongoHistoryProcessor(
        IMongoCollection<BsonDocument> source,
        IMongoCollection<HistoryEntry> history,
        MongoHistoryOptions options,
        IHistoryCheckpointStore checkpoints,
        HistoryHealthState state,
        ILogger logger)
    {
        _source = source;
        _history = history;
        _options = options;
        _mapper = new HistoryMapper(options);
        _checkpoints = checkpoints;
        _state = state;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // The position this run owns: the token it started from, then each token it saves. A gap clears only that
        // position, never a newer one written by another consumer.
        var ownToken = await _checkpoints.GetResumeTokenAsync(cancellationToken);
        var streamOptions = new ChangeStreamOptions
        {
            FullDocument = ChangeStreamFullDocumentOption.WhenAvailable,
            FullDocumentBeforeChange = ChangeStreamFullDocumentBeforeChangeOption.WhenAvailable,
            StartAfter = ownToken,
            MaxAwaitTime = _options.MaxAwaitTime,
            BatchSize = _options.BatchSize,
        };

        // No $match: invalidate/drop events must stay visible.
        var pipeline = new EmptyPipelineDefinition<ChangeStreamDocument<BsonDocument>>();

        try
        {
            using var cursor = await _source.WatchAsync(pipeline, streamOptions, cancellationToken);
            while (await cursor.MoveNextAsync(cancellationToken))
            {
                var events = cursor.Current
                    .Where(IsHandledOperation)
                    .Select(ToEvent)
                    .ToList();
                events = await FillMissingPostImagesAsync(events, cancellationToken);

                var rows = events
                    .Select(_mapper.Map)
                    .OfType<HistoryEntry>()
                    .ToList();

                if (rows.Count > 0)
                    await InsertIgnoringDuplicatesAsync(rows, cancellationToken);

                // Saved after EVERY batch, including empty ones: the post-batch token keeps the saved position
                // inside the oplog even when the source is idle while other collections churn.
                var token = cursor.GetResumeToken();
                if (token is not null)
                {
                    await _checkpoints.SaveResumeTokenAsync(token, cancellationToken);
                    ownToken = token;
                    _state.MarkCheckpoint();
                }
            }

            // Closed stream (invalidate after a drop/rename): the token saved with the batch that carried the
            // invalidate event is valid for StartAfter, so the next run resumes without losing events.
        }
        catch (MongoCommandException ex) when (ex.Code == ChangeStreamHistoryLostCode)
        {
            _logger.LogError(ex, "{SourceName} history gap: resume token no longer in the oplog, restarting from now", _options.SourceName);
            await _checkpoints.ClearResumeTokenAsync(ownToken, cancellationToken);
        }
    }

    // Loads the current documents with one query per batch. A document can already include later changes.
    private async Task<List<ChangeEvent>> FillMissingPostImagesAsync(
        List<ChangeEvent> events, CancellationToken cancellationToken)
    {
        var missingIds = events
            .Where(e => e.Operation == ChangeStreamOperationType.Update && e.After is null)
            .Select(e => e.EntityId)
            .Distinct()
            .ToList();
        if (missingIds.Count == 0)
            return events;

        using var cursor = await _source.FindAsync(
            Builders<BsonDocument>.Filter.In("_id", missingIds), cancellationToken: cancellationToken);
        var current = (await cursor.ToListAsync(cancellationToken)).ToDictionary(d => d["_id"]);

        _logger.LogInformation(
            "Loaded {LoadedCount} of {MissingCount} {SourceName} documents without post-image for the history",
            current.Count, missingIds.Count, _options.SourceName);

        return events
            .Select(e => e.Operation == ChangeStreamOperationType.Update && e.After is null && current.TryGetValue(e.EntityId, out var document)
                ? e with { After = document }
                : e)
            .ToList();
    }

    private static bool IsHandledOperation(ChangeStreamDocument<BsonDocument> change) =>
        change.OperationType is ChangeStreamOperationType.Insert
            or ChangeStreamOperationType.Update
            or ChangeStreamOperationType.Replace
            or ChangeStreamOperationType.Delete;

    private static ChangeEvent ToEvent(ChangeStreamDocument<BsonDocument> change) => new(
        change.OperationType,
        change.DocumentKey["_id"],
        change.FullDocumentBeforeChange,
        change.FullDocument,
        change.UpdateDescription?.UpdatedFields,
        change.UpdateDescription?.RemovedFields,
        change.ResumeToken["_data"].AsString,
        // WallTime has millisecond resolution (server 6.0+); ClusterTime only seconds.
        change.WallTime ?? DateTimeOffset.FromUnixTimeSeconds(change.ClusterTime.Timestamp).UtcDateTime,
        change.UpdateDescription?.TruncatedArrays?
            .OfType<BsonDocument>()
            .Select(a => a["field"].AsString)
            .ToList());

    private async Task InsertIgnoringDuplicatesAsync(List<HistoryEntry> rows, CancellationToken cancellationToken)
    {
        try
        {
            await _history.InsertManyAsync(rows, new InsertManyOptions { IsOrdered = false }, cancellationToken);
        }
        catch (MongoBulkWriteException<HistoryEntry> ex)
            when (ex.WriteErrors.Count > 0 && ex.WriteErrors.All(e => e.Category == ServerErrorCategory.DuplicateKey))
        {
            _logger.LogInformation(
                ex, "Skipped {SkippedCount} already-recorded {SourceName} history rows", ex.WriteErrors.Count, _options.SourceName);
        }
    }
}
