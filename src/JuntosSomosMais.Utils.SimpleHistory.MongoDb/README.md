# JuntosSomosMais.Utils.SimpleHistory.MongoDb

Change history for MongoDB collections, built on change streams. It is the .NET analogue of
[django-simple-history](https://django-simple-history.readthedocs.io/): every insert, replace, update and delete of
a tracked collection is recorded as a row in a dedicated history collection (a delta of what changed, or the whole
document, per collection), without touching the code that writes to the tracked collection (raw bulk writes, legacy
writers and other apps are captured too).

Any number of collections can be tracked. Each one has its own history collection, options, resume token and
hosted service, so they run independently.

This is the MongoDB provider of `JuntosSomosMais.Utils.SimpleHistory` (referenced transitively). The core package holds the
neutral parts: `HistoryOptions`, `HistoryBuilder<TOptions>`, `HistorySources`, `HistoryHostedService`, the
`IHistorySetup` / `IHistoryProcessor` / `IHistoryPurger` contracts and the row contract every provider follows. The
types below that start with `Mongo` are the implementations of those contracts for MongoDB.

## Requirements

- MongoDB 6.0 or newer, as a **replica set or sharded cluster** (change streams do not exist on a standalone) with
  WiredTiger.
- Privileges of the user that runs it: `changeStream` and `find` on the tracked collection; read/write and
  `createIndex` on the history and checkpoint collections; `createCollection`; and `collMod` on the tracked
  collection (needed once, to enable pre/post-images). Run `MongoHistorySetup.EnsureAsync` with an admin user if the worker's
  user should not have `collMod`.
- Guid `_id` values: configure the client with `GuidRepresentation.Standard`, otherwise driver 2.x rewrites the UUID
  subtype between the source and the history row.

## Setting up a consumer

Three things have to be true before rows appear: the collections and indexes exist (**setup**), exactly one
deployment consumes the change streams (**worker**), and the Mongo user of each step has the privileges listed above.
The recommended shape is one class that declares the tracked collections, used by both the worker and the setup tool.

### 1. Declare the tracked collections once

```csharp
// Infrastructure/History/MyOrdersHistorySources.cs
using JuntosSomosMais.Utils.SimpleHistory.MongoDb;

public static class MyOrdersHistorySources
{
    /// <summary>The single source of truth: used by the worker and by the setup tool.</summary>
    public static void Configure(MongoHistoryBuilder history) => history
        .Track("myOrders", o =>                                   // own history collection: myOrdersHistory
        {
            o.IgnoreFields("CreationDate");
            o.StoreFullDocument = true;                            // every "~" row holds the whole order
        })
        .Track("carts", o => o.CompressHistory = false);           // delta rows, defaults for everything else

    /// <summary>The validated options (throws InvalidOperationException on an invalid declaration).</summary>
    public static IReadOnlyList<MongoHistoryOptions> Build()
    {
        var history = new MongoHistoryBuilder();
        Configure(history);
        return history.Build();
    }
}
```

### 2. Run the setup (collections, pre/post-images, indexes)

`MongoHistorySetup` is idempotent: it creates what is missing and leaves what exists untouched (an existing history
collection keeps its storage options). It needs `createCollection`, `createIndex` and, once per source, `collMod`.
Pick the place that has those privileges:

- **In the worker, automatically.** `AddMongoHistory` (step 3) runs the setup when each hosted service starts and
  again after a failure. Nothing else is needed when the worker's user has the privileges.
- **From a tool or migration, before the first start.** Use this when the worker's user must not have `collMod`, or
  when you want the collections to exist before deploying the worker (this repo does it in its `create-indexes`
  console command):

  ```csharp
  var database = new MongoClient(connectionString).GetDatabase(databaseName);
  await MongoHistorySetup.EnsureAsync(database, MyOrdersHistorySources.Build(), cancellationToken);
  ```

  `EnsureAsync` validates every declared source before touching the database, then sets them up one by one.
- **At the start of another host** (an API that only reads history, a migration job) without consuming the streams:
  a plain hosted service around the same call.

  ```csharp
  public sealed class HistorySetupHostedService(IMongoDatabase database) : IHostedService
  {
      public Task StartAsync(CancellationToken ct) =>
          MongoHistorySetup.EnsureAsync(database, MyOrdersHistorySources.Build(), ct);

      public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
  }

  services.AddHostedService<HistorySetupHostedService>();
  ```

After the setup, `db.getCollectionInfos({ name: "myOrders" })[0].options.changeStreamPreAndPostImages.enabled` is
`true`, `myOrdersHistory` exists (with `block_compressor=zstd` unless disabled) with the three indexes listed below,
and the shared `simpleHistoryCheckpoint` collection exists.

### 3. Host the change stream consumers in exactly one deployment

```csharp
// Workers/History/MyOrdersHistoryRegistrationExtension.cs
using JuntosSomosMais.Utils.SimpleHistory;
using JuntosSomosMais.Utils.SimpleHistory.MongoDb;

public static IServiceCollection RegisterMyOrdersHistory(this IServiceCollection services, string databaseName)
{
    services.AddMongoHistory(
        sp => sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
        MyOrdersHistorySources.Configure);
    services.AddHealthChecks().AddSimpleHistoryHealthCheck(); // opt-in, see Operational notes
    return services;
}
```

`AddMongoHistory` resolves the database lazily (when the hosted services start), validates the declaration at
registration and registers:

- one `HistoryHostedService` per tracked collection (setup, change stream), so a failing source never blocks
  another;
- a `HistorySources` singleton with the options of every tracked collection;
- a `HistoryHealthRegistry` singleton with the health state of every tracked collection, read by the health check;
- `IHistoryPurger` (see Maintenance).

Call it once per service, with every tracked collection in the same declaration: a second call throws.

It registers no health check: chain `AddSimpleHistoryHealthCheck(name = "simplehistory", failureStatus = null,
tags = null)` on `services.AddHealthChecks()` to expose one.

Call it in **one** deployment with **exactly one replica**, for example a worker started with a dedicated argument
(Kubernetes: `replicas: 1`, strategy `Recreate`, so a rollout never runs two pods at once). There is no lease or
leader election. If two consumers ever overlap they write the same rows and the unique `ChangeToken` index drops the
duplicates, so the history stays correct; only the checkpoint position may briefly move back and forth.

### 4. Verify

1. Start the worker, then insert and update a tracked document.
2. `db.myOrdersHistory.find({ EntityId: <id> }).sort({ HistoryDate: 1 })` shows a `+` row and a `~` row whose `Data`
   holds only the changed fields (delta mode) or the whole document plus `ChangedFields` (full-document mode).
3. `db.simpleHistoryCheckpoint.findOne({ _id: "myOrders" })` has a `ResumeToken`.
4. Stop the worker, change a document, start it again: the missed change appears exactly once.

## What setup does (idempotent, repeated on every worker start)

1. Creates the tracked collection if missing and enables `changeStreamPreAndPostImages` on it.
2. Creates the history collection (`block_compressor=zstd` unless `CompressHistory = false`) before its indexes.
3. Creates the shared checkpoint collection.
4. Creates three indexes on the history collection, named after it: `{history}-entity-id-history-date`
   (`EntityId`, `HistoryDate` desc, `ChangeToken` desc), the unique `{history}-change-token` (`ChangeToken`) and
   `{history}-history-date` (`HistoryDate`, used by the purge below).

## Options (`MongoHistoryOptions`, per tracked collection)

`MongoHistoryOptions` derives from the core `HistoryOptions` and adds `CompressHistory` and `MaxAwaitTime`. The
fluent `IgnoreFields(...)` / `TrackFields(...)` keep the Mongo type so chaining works.

| Option | Default | Meaning |
|---|---|---|
| `SourceName` | required | Collection being tracked |
| `HistoryName` | `{source}History` | Where rows go. Two sources cannot share one |
| `CheckpointName` | `simpleHistoryCheckpoint` | Resume token, one document per source, shared |
| `CheckpointId` | the source name | `_id` of the source's checkpoint document |
| `IgnoredFields` / `IgnoreFields(...)` | none | Top-level fields that never count as a change (case sensitive); still stored in full rows |
| `TrackedFields` / `TrackFields(...)` | none (= every field) | Whitelist of the top-level fields stored in every row; a change outside it records nothing. Cannot overlap `IgnoredFields`; `_id`, nested paths (`Address.City`) and `$` names are rejected |
| `StoreFullDocument` | `false` | `true`: `~` rows store the whole document plus `ChangedFields` instead of a delta |
| `CompressHistory` | `true` | zstd on the history collection |
| `MaxAwaitTime` | 5 s | Server-side wait of one stream batch |
| `BatchSize` | 100 | Change stream batch size |
| `InitialBackoff` / `MaxBackoff` | 5 s / 60 s | Retry delay of the hosted service after a failure |
| `UnhealthyAfter` | 5 min | Time without a saved checkpoint after which the health check reports Unhealthy; must be longer than `MaxAwaitTime` and `MaxBackoff` |

Invalid options, duplicate sources, a shared history collection or a history/checkpoint collection that is itself
tracked throw `InvalidOperationException` at registration, not at runtime.

## Row shape (`HistoryEntry`)

| Field | Meaning |
|---|---|
| `_id` | Row id (`ObjectId`) |
| `EntityId` | `_id` of the tracked document, any BSON type (ObjectId, string, Guid, int...) |
| `HistoryDate` | Wall time of the change (UTC) |
| `HistoryType` | `+` created, `~` changed, `=` full snapshot (replace without pre-image), `-` deleted |
| `Data` | `+`, `=`, `-`: the full document without `_id` (only the tracked fields when `TrackedFields` is set). `~`: only the changed top-level fields with their new values, or the whole document in full-document mode |
| `RemovedFields` | `~` only: top-level fields that were removed |
| `ChangedFields` | Full-document mode only: the top-level fields whose value changed (may be empty when only fields were removed). Its presence on a `~` row means `Data` is the whole document |
| `ChangeToken` | Resume token of the event, i.e. its oplog position; unique (makes inserts idempotent) and the ordering tiebreaker for equal `HistoryDate` |

Ignored fields never count as a change (a change that only touches them records nothing) and are left out of delta
rows; `+`, `=`, `-` and full-document `~` rows carry them. With `TrackedFields` every row keeps only the listed
fields.

**Delta vs full document.** Delta mode (default) stores the minimum and a version is read by replaying the chain
(`HistoryReplay.Rebuild`). Full-document mode (`StoreFullDocument = true`) stores the document on every change: each
`~` row is a readable version by itself, `ChangedFields` tells what changed, and a replay simply takes the last row.
Pick full mode when rows are read ad hoc (support, audits) and the document is small enough; pick delta mode when
storage matters more than read convenience.

## Reading and restoring

```csharp
var rows = await database.GetCollection<HistoryEntry>("myOrdersHistory")
    .Find(Builders<HistoryEntry>.Filter.Eq(h => h.EntityId, new BsonObjectId(id)))
    .SortBy(h => h.HistoryDate).ThenBy(h => h.ChangeToken)
    .ToListAsync();

BsonDocument current = HistoryReplay.Rebuild(rows);                       // latest version (without _id)
BsonDocument atRow   = HistoryReplay.Rebuild(rows, rows[2].ChangeToken);  // as of a given row
```

`Rebuild` works for both row shapes: a delta `~` row is merged into the state, a full-document `~` row (one with
`ChangedFields`) replaces it. In full-document mode the `Data` of any `~` row is already that version. A chain that
ends with a `-` row yields the last version before the delete; when the same `_id` is inserted again, the rows after
the `-` form a new chain and `Rebuild` yields the re-inserted document.

## Operational notes

- **Asynchronous.** A row appears milliseconds to seconds after the write and is not part of the write's transaction.
- **No actor.** A change stream cannot know who wrote; rows record what and when.
- **Resume token.** The consumer saves the resume token after every batch (also empty ones) in the checkpoint
  document `{ _id: <source>, ResumeToken }`. A restarted worker resumes exactly where it stopped; redelivered events
  are ignored thanks to the unique `ChangeToken` index.
- **One replica.** There is no lease: run the consuming deployment with one replica and the `Recreate` strategy. An
  accidental overlap only produces duplicates that the `ChangeToken` index rejects.
- **Health check.** `services.AddHealthChecks().AddSimpleHistoryHealthCheck()` adds one check (`simplehistory` by
  default) for all tracked collections. It reports the registration's failure status (Unhealthy by default) when any
  collection saved no checkpoint for its `UnhealthyAfter` (default 5 min; a checkpoint is saved after every batch, so
  an idle collection still checkpoints every `MaxAwaitTime`). The description lists the stale collections with their
  age and the exception type of the last loop error (not its message, which can carry host names; the full exception
  is in the log); `Data` has the age of every collection. Expose it on the endpoint that serves its tags (untagged by
  default) and alert on it; a stuck or crash-looping consumer shows up there before the lag does. A worker that gets
  no traffic gains nothing from failing readiness: pass `tags` that its liveness endpoint also serves, so a stuck
  stream restarts the pod.
- **Gap risk.** If the consumer is down longer than the oplog window / pre-image retention, the stream cannot resume
  (error 286). It is logged at error level, the token is cleared (only if it is still the one this run started from
  or last saved, so a stale consumer never wipes a newer position) and the stream restarts from now; the missed window
  is not recoverable. This is the only case that restarts from now: when the stream closes by itself (an `invalidate`
  after a drop or rename of the tracked collection) the token saved with that batch is kept and the next run resumes
  from it with `StartAfter`.
- **Pre-image retention.** Pre-images cost storage and oplog size. Bound them with
  `db.adminCommand({ setClusterParameter: { changeStreamOptions: { preAndPostImages: { expireAfterSeconds: 3600 } } } })`
  (some managed tiers restrict it).
- **Missing images.** When an update has no post-image, the current documents are loaded with one query per batch.
  Top-level fields set or removed by the update keep their exact values from the update description; only fields
  changed through a nested path (or a truncated array) are read from the current document, so they may already
  reflect a later change, which the later event records again. When the document is already gone, the row keeps only
  the top-level changes.
- **Large events.** With pre- and post-images, a replace carries two full documents; events above 16 MB stall the
  consumer (error 10334). On server 7.0+ `$changeStreamSplitLargeEvent` would be needed.
- No backfill: history starts when tracking starts.

## Maintenance (`IHistoryPurger`)

`MongoHistoryPurger` (behind `IHistoryPurger`, registered by `AddMongoHistory` or constructed with the
`IMongoDatabase`) deletes old rows by date, for a list of `HistoryOptions`:

```csharp
IHistoryPurger purger = new MongoHistoryPurger(database);
var results = await purger.PurgeAsync(
    historySources.Options,                                           // a HistorySources instance, or any subset
    new HistoryPurgeRequest(FromUtc: null, ToUtc: DateTime.UtcNow.AddDays(-365), DryRun: true));
// results[i]: HistoryName, Matched, Deleted (0 on a dry run), DryRun
```

- The interval is `FromUtc <= HistoryDate < ToUtc`: **from is inclusive, to is exclusive**. Either side may be open,
  but **at least one bound is required**: the purger never deletes every row. `FromUtc` must be before `ToUtc`. An
  open `ToUtc` is pinned to the time the purge starts (reported as `HistoryPurgeResult.ToUtc`), so rows written while
  it runs are kept.
- Bounds must be `DateTimeKind.Utc` (a `Local` value is converted, `Unspecified` is rejected).
- `DryRun = true` only counts (`Matched`) and deletes nothing. Run it first.
- Rows are deleted in batches of `BatchSize` (default 5000): each round selects the ids of one batch through the
  `HistoryDate` index and deletes exactly those, so a large purge never holds one long-running delete and the oplog
  grows in bounded steps. Results come in input order. A history collection that does not exist yields `Matched = 0`
  and is not created. The tracked (source) collection and the checkpoint collection are never touched.
- The `{history}-history-date` index created by `MongoHistorySetup` serves the range delete; run the setup once after
  upgrading so existing collections get it.
- **Replay limit (delta mode only).** Rows are plain-deleted, chains are not compacted. In delta mode, when the oldest
  rows of an entity (its `+` or `=` base row) are removed, the remaining `~` deltas can no longer be replayed and
  `HistoryReplay.Rebuild` throws for that entity. In full-document mode every remaining row is a readable version by
  itself and `Rebuild` keeps working; only the purged versions are gone. Purge only when they are no longer needed.

### Purge command (`PurgeHistoryCommand`)

The core package ships the command-line front end of the purger, so an app only wires a command name to it:

```csharp
// args: what follows the command name, e.g. ["--collections=myOrders", "--older-than-days=90", "--dry-run"]
var exitCode = await PurgeHistoryCommand.ExecuteAsync(
    args,
    sources,                                             // the HistoryOptions the command may purge
    () => new MongoHistoryPurger(database),              // only called once the arguments are valid
    Console.Out);
```

| Argument | Meaning |
|---|---|
| (none) | `--older-than-days=365` on every given source |
| `--collections=a,b` | Only these. Each name matches a history collection **or** its source collection, case-insensitive. An unknown name throws `ArgumentException` listing the valid names |
| `--older-than-days=N` | Rows older than `now(UTC) - N days`. `N` must be a positive integer; cannot be combined with `--from`/`--to` |
| `--from=<date>` | Inclusive lower bound on `HistoryDate` |
| `--to=<date>` | **Exclusive** upper bound on `HistoryDate` |
| `--dry-run` | Count what would be deleted, delete nothing |

Dates are UTC; a date-only value means midnight UTC. Unknown, repeated, empty or inconsistent arguments throw
`ArgumentException` before the purger is created. It prints one line per history collection and the total
(`myOrdersHistory: 1542 rows older than 2025-10-05T16:50:00Z would be deleted (dry run)`, `Total: 1542`) and returns
exit code 0. `PurgeHistoryArgs.Parse` and `PurgeHistoryCommand.RunAsync` are public for apps that parse or build the
purger themselves.

## Porting the library to another app

1. Add the package `JuntosSomosMais.Utils.SimpleHistory.MongoDb` (it brings the core
   `JuntosSomosMais.Utils.SimpleHistory`).
2. Follow "Setting up a consumer" above: one declaration class, the setup where the privileges are, `AddMongoHistory`
   in one deployment.
3. Set the pre-image retention cluster parameter and monitor: the `simplehistory` health check, the `history gap`
   error log, lag between now and the newest `HistoryDate`, and `history stream failed` errors.
