# JuntosSomosMais.Utils.RequestAudit.MongoDb

MongoDB provider of [JuntosSomosMais.Utils.RequestAudit](../JuntosSomosMais.Utils.RequestAudit/README.md): one document per audited request.

Built against `MongoDB.Driver` 2.x (`[2.29.0,3.0.0)`).

## Setup

```csharp
services.AddRequestAudit();
services.AddMongoRequestAudit(
    provider => provider.GetRequiredService<IMongoDatabase>(),
    options => options.CollectionName = "requestAuditLogs"); // default
```

It registers the store (`IRequestAuditStore`) and the purger (`IRequestAuditPurger`). The database is resolved lazily, when each is first used. Calling `AddMongoRequestAudit` twice throws.

## Indexes

Registration does not create indexes. Run the setup from the tool that prepares your database (a console command, a migration step), with the same options:

```csharp
await MongoRequestAuditSetup.EnsureAsync(database, options);
```

It is idempotent and creates indexes on `CreatedAtUtc` (used by the purge), `Url`, `IpAddress` and `RequestId`.

## Retention

There is no TTL index: entries stay until they are purged. `MongoRequestAuditPurger` deletes by `CreatedAtUtc` range in batches (it selects the ids of one batch, then deletes exactly those), so a large purge never holds one long-running delete. Run it through the core's `PurgeRequestAuditCommand`, whose arguments, the mandatory scheduling and a Kubernetes CronJob example are in the [core README](../JuntosSomosMais.Utils.RequestAudit/README.md#retention):

```csharp
return await PurgeRequestAuditCommand.ExecuteAsync(
    args, () => new MongoRequestAuditPurger(database, options), Console.Out);
```

```bash
dotnet MyApp.dll purge-request-audit --older-than-days=30 --dry-run
dotnet MyApp.dll purge-request-audit --older-than-days=30
```

## Document shape

```json
{
  "_id": "6e503845-81ae-48f0-8336-7eb012374283",
  "CreatedAtUtc": "2026-11-10T13:08:44.509Z",
  "Method": "POST",
  "Url": "https://api.example.com/v2/webhooks/order",
  "QueryParams": [{ "Name": "source", "Value": "partner" }],
  "IpAddress": "203.0.113.7",
  "RequestHeaders": [{ "Name": "Authorization", "Value": "***" }],
  "RequestBody": "{\"id\":\"1\",\"email\":\"***\"}",
  "RequestBodyNote": null,
  "ResponseStatusCode": 200,
  "ResponseHeaders": [{ "Name": "Content-Type", "Value": "application/json" }],
  "ResponseBody": "{\"status\":\"ok\"}",
  "ResponseBodyNote": null,
  "RequestId": "0HNPV5EVSI4SP:00000001",
  "TraceParent": "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
  "Caller": [{ "Name": "cid", "Value": "partner-client" }]
}
```

- `_id` is the entry id as text, so it does not depend on the Guid representation the app configured.
- Headers, query parameters and caller claims are lists of `Name`/`Value` pairs, because their names may contain `.` or start with `$`. Query one with `$elemMatch`.
- Bodies are stored as JSON text, already masked. Atlas snapshots (backups) keep purged documents until each snapshot expires, so the real retention is the purge window plus the snapshot retention.
