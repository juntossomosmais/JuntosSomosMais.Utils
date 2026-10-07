# JuntosSomosMais.Utils.SimpleHistory

Provider-neutral core of **SimpleHistory**, the .NET analogue of
[django-simple-history](https://django-simple-history.readthedocs.io/): every insert, replace, update and delete of a
tracked source (a collection, a table) is recorded as a compact row in a dedicated history store, without touching the
code that writes to the source.

This package holds what does not depend on a database: the options, the fluent builder that declares the tracked
sources, the hosted service that runs one consumer per source with retry and exponential backoff, and the contracts a
database provider implements. It has no database dependency. Applications reference a **provider** package, which
pulls this one transitively:

| Provider | Package | Status |
|---|---|---|
| MongoDB (change streams) | `JuntosSomosMais.Utils.SimpleHistory.MongoDb` | available |
| SQL Server (change tracking / CDC) | `JuntosSomosMais.Utils.SimpleHistory.SqlServer` | not implemented |
| PostgreSQL (logical decoding or triggers) | `JuntosSomosMais.Utils.SimpleHistory.PostgreSql` | not implemented |
| Entity Framework Core (`SaveChangesInterceptor`) | `JuntosSomosMais.Utils.SimpleHistory.EntityFrameworkCore` | not implemented |

## Row contract

Every provider records rows with the same meaning, so readers and tooling can treat history the same way whatever the
database. One row describes one change of one entity:

| Field | Meaning |
|---|---|
| row id | Unique row id |
| `EntityId` | Identifier of the tracked entity, in the provider's native type |
| `HistoryDate` | UTC time of the change, as seen by the database |
| `HistoryType` | One of `HistoryTypes`: `+` created, `~` changed, `=` snapshot, `-` deleted |
| `Data` | `+`, `=`, `-`: the full entity without its id (only the tracked fields when `TrackedFields` is set). `~`: only the changed top-level fields with their new values, or the whole entity in full-document mode |
| `RemovedFields` | `~` only: top-level fields that no longer exist |
| `ChangedFields` | Full-document mode only: the top-level fields whose value changed. Its presence on a `~` row means `Data` is the whole entity, not a delta |
| `ChangeToken` | Provider-specific position of the change; unique (makes inserts idempotent on redelivery) and the ordering tiebreaker for equal `HistoryDate` |

Rules a provider follows:

- `+` for a creation, `-` for a deletion (with the last known version when the database can supply it).
- A modification whose previous version is known produces a `~` delta; when nothing changed it produces **no row**.
- A full rewrite whose previous version is unknown produces an `=` snapshot. A partial update whose previous version
  is unknown produces a `~` built from the fields the database reports as touched.
- Fields listed in `HistoryOptions.IgnoredFields` never count as a change (a change that touches only them records
  nothing) and are left out of delta rows; full rows (`+`, `=`, `-` and full-document `~`) still carry them.
- When `TrackedFields` is set, every row keeps only those fields and a change that touches only other fields records
  nothing.
- **Row shape.** Delta mode (default): a `~` row holds only what changed, so storage is minimal and reading a version
  means replaying the chain. Full-document mode (`StoreFullDocument = true`): a `~` row holds the whole entity plus
  `ChangedFields`, so every row is a readable version by itself at the cost of storing the document on every change.
- Replaying a chain (`+` or `=` then `~`...) rebuilds any version; a full-document `~` row replaces the state instead
  of being merged. In delta mode, deleting the oldest rows of an entity breaks its chain.

## Options (`HistoryOptions`, per tracked source)

| Option | Default | Meaning |
|---|---|---|
| `SourceName` | required | Source being tracked |
| `HistoryName` | `{source}History` | Store that receives the rows. Two sources cannot share one |
| `CheckpointName` | `simpleHistoryCheckpoint` | Resume position, one entry per source, shared |
| `CheckpointId` | the source name | Id of the source's checkpoint entry |
| `IgnoredFields` / `IgnoreFields(...)` | none | Top-level fields that never count as a change (case sensitive); still stored in full rows |
| `TrackedFields` / `TrackFields(...)` | none (= every field) | Whitelist of the top-level fields stored in every row; a change outside it records nothing. A field cannot be both tracked and ignored, `_id` can be neither, and a nested path (`Address.City`) or a `$` name is rejected |
| `StoreFullDocument` | `false` | `true`: `~` rows store the whole entity plus `ChangedFields` instead of a delta |
| `BatchSize` | 100 | Changes read per batch |
| `InitialBackoff` / `MaxBackoff` | 5 s / 60 s | Retry delay of the hosted service after a failure |
| `UnhealthyAfter` | 5 min | Time without a saved checkpoint after which `HistoryHealthCheck` reports Unhealthy; must be longer than `MaxBackoff` |

Providers derive from `HistoryOptions` to add their settings and override `Validate()`, calling the base first.
`HistoryBuilder<TOptions>` collects the sources and rejects, at registration time: no source, a duplicate source,
invalid options, two sources sharing a history store, a source that is also a history or checkpoint store, and a
checkpoint store that is also a history store.

## Lifecycle (`HistoryHostedService`)

One hosted service per tracked source, so a failing source never blocks another. It runs the provider's
`IHistorySetup.EnsureSetupAsync` once, then `IHistoryProcessor.RunAsync` in a loop. A clean return (the feed closed, a
gap was detected) is not a failure: the service waits `InitialBackoff`, repeats the setup (a dropped and recreated
source needs its preparation again) and tries again. An exception is logged, the setup is repeated and the delay
doubles up to `MaxBackoff`. An error raised while the host is stopping is not logged.

There is no lease or leader election: the deployment that hosts the consumers must run **exactly one replica**
(Kubernetes: `replicas: 1` with the `Recreate` strategy, so a rollout never runs two pods at once). If two consumers
ever overlap, both write the same rows and the unique `ChangeToken` index drops the duplicates, so the history stays
correct; only the checkpoint position may briefly move back and forth.

## Monitoring (`HistoryHealthCheck`)

Every source has an in-process `HistoryHealthState`: the processor marks it after each saved checkpoint (empty
batches included, so an idle source still checkpoints every batch wait) and the hosted service records the last
failure. The provider collects every source's state in a `HistoryHealthRegistry` singleton.

`HistoryHealthCheck` (an `IHealthCheck`) is **one check for all sources**. A source is stale when it saved no
checkpoint for longer than its `UnhealthyAfter`, counted from the process start until the first one. With no stale
source the check is Healthy; otherwise it reports the registration's failure status (Unhealthy unless set) with one
line per stale source, separated by ` | `, carrying the last error's exception **type** (never its message, which can
carry host names; the full exception is logged):
`myOrders: no checkpoint for 0:06:12 (since start, no checkpoint yet); last error: MongoConnectionException (at ...)`.
`Data` has one entry per source (`0:00:04 since the last checkpoint`). It needs no database round trip and no clock
comparison between machines, and it covers a setup that keeps failing, a stream that cannot be opened and a hung
cursor.

The check is opt-in, registered by the app on its health checks builder:

```csharp
services.AddHealthChecks().AddSimpleHistoryHealthCheck();
// name = "simplehistory", failureStatus = null (Unhealthy), tags = none; all three can be set:
// .AddSimpleHistoryHealthCheck("history", HealthStatus.Degraded, ["be"]);
```

## Writing a provider

A provider package references this one and ships:

- `XxxHistoryOptions : HistoryOptions` and `XxxHistoryBuilder : HistoryBuilder<XxxHistoryOptions>` (with a covariant
  `new Track(...)` so chaining keeps the derived type).
- `IHistorySetup`: idempotent preparation of the source, history and checkpoint stores and their indexes.
- `IHistoryProcessor.RunAsync(ct)`: consume the change feed, write rows idempotently, save the position after every
  batch (empty ones included). Return when the feed closes or a gap is detected; let cancellation propagate.
- A resume-position store. Its shape is provider-specific (the MongoDB one keeps the resume token as a BSON document);
  the core imposes no token type.
- `IHistoryPurger`: delete rows of the listed sources by `HistoryDate` range, never touching the source or checkpoint
  stores, honouring `HistoryPurgeRequest` (half-open interval, at least one bound, dry run) and returning one
  `HistoryPurgeResult` per source in input order. The core's `PurgeHistoryCommand` (argument parsing, collection
  selection, report) runs on top of any `IHistoryPurger`; the MongoDB README documents its arguments.
- `AddXxxHistory(services, dbFactory, configure)`: builds the options, registers a `HistorySources` singleton, one
  `IHostedService` (`HistoryHostedService`) per source, a `HistoryHealthRegistry` singleton (one `HistoryHealthState`
  per source, shared by its processor and hosted service) and the `IHistoryPurger`. It registers no health check:
  the app opts in with `AddSimpleHistoryHealthCheck()`.

The MongoDB provider is the reference implementation; its README documents the end-user experience.
