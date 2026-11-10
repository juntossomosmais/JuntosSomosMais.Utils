# JuntosSomosMais.Utils.RequestAudit

Opt-in HTTP request auditing for ASP.NET Core. For every request to an endpoint you mark, it stores one entry with who called, what was sent and what was answered. Sensitive values are masked **before** the entry is stored.

This is the provider-neutral core. To store the entries in MongoDB, add [JuntosSomosMais.Utils.RequestAudit.MongoDb](../JuntosSomosMais.Utils.RequestAudit.MongoDb/README.md).

## Setup

```csharp
// Services
services.AddRequestAudit(options =>
{
    options.ClientIpHeader = "True-Client-IP"; // when the app is behind a proxy
    options.CallerClaims.Add("cid");
});
services.AddMongoRequestAudit(provider => provider.GetRequiredService<IMongoDatabase>());

// Pipeline: after routing, before authentication
app.UseRouting();
app.UseRequestAudit();
app.UseAuthentication();
app.UseAuthorization();
```

The position matters: after `UseRouting` the endpoint is known, and before `UseAuthentication` a 401 or 403 is audited too.

Mark what should be audited:

```csharp
[HttpPost, AuditRequest]
public Task<IActionResult> Post([FromBody] OrderRequest request) { ... }

// Minimal APIs
app.MapPost("/orders", handler).WithMetadata(new AuditRequestAttribute());
```

`[AuditRequest]` on a controller audits every action. Endpoints without it are not touched at all.

## What is stored

`RequestAuditEntry`:

| Field | Content |
|---|---|
| `Id`, `CreatedAtUtc` | Identifier and arrival time of the request. |
| `Method`, `Url`, `QueryParams` | The URL is scheme, host and path; the query string is kept apart. |
| `IpAddress` | `ClientIpHeader` when set and present, otherwise the connection address. |
| `RequestHeaders`, `RequestBody`, `RequestBodyNote` | What was received. |
| `ResponseStatusCode`, `ResponseHeaders`, `ResponseBody`, `ResponseBodyNote` | What was answered. |
| `RequestId`, `TraceParent` | `HttpContext.TraceIdentifier` and the `traceparent` header. |
| `Caller` | The claims listed in `CallerClaims` that the authenticated user has. |

## Masking

| What | Rule |
|---|---|
| Headers in `SensitiveHeaders` | Value replaced by `***`. Default: `Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie`, `X-Api-Key`, `X-Auth-Token`, `X-Access-Token`, `Api-Key`. |
| JSON properties (any depth), form fields and query parameters in `SensitiveFields` | Value replaced by `***`. Default: `cpf`, `cnpj`, `email`, `phone`, `telefone`, `celular`, `cvv`, `authorization`. |
| Fields and headers whose name contains one of `SensitiveNameParts` | Value replaced by `***`. Default: `password`, `senha`, `secret`, `token`, `apikey`. |

Header names in `SensitiveHeaders` are compared exactly, ignoring case. Field names, and header names checked against `SensitiveNameParts`, are compared on a normalized form, on both sides: the last segment of the key (after the last `.` or `[`, numeric indexes skipped), without `_` and `-`, ignoring case. So `card_number` also masks `cardNumber` and `Payment.CardNumber`, a field in `SensitiveFields` also masks its plural (`emails`), and the part `token` masks `access_token`, `idToken`, `tokens` and `X-CSRF-Token`. Add your own to any of the sets.

Not masked:

- the URL path (only the query string goes through `SensitiveFields`), so keep secrets out of route values;
- the `Referer` header, which can carry another page's query string; add it to `SensitiveHeaders` if that matters.

`CallerClaims` matches claim types as they are on the `ClaimsPrincipal`. With JwtBearer's default `MapInboundClaims = true`, the `sub` claim arrives as `ClaimTypes.NameIdentifier`, so either add that type to `CallerClaims` or set `MapInboundClaims = false`.

A body is stored only when it can be masked: JSON (`application/json`, `+json`) and `application/x-www-form-urlencoded` (stored as a JSON object). It is **not** stored, and the `...BodyNote` field says why, when:

- the content type is anything else (multipart, plain text, binary);
- the JSON is invalid;
- it is larger than `MaxBodyBytes` (default 32 KB, at most 1 MB), because a cut body cannot be parsed and masked.

## Behavior to know

- **The entry is saved after the response is sent** (`Response.OnCompleted`), so the caller does not wait for the store. On HTTP/1.1, the server reads the next request on the same connection only after the save, so a slow store can delay that request, but never longer than `SaveTimeout` (default 5 s): the save is then cancelled and the failure logged, even when the store ignores the cancellation.
- **Auditing never breaks the request.** If the store fails, the error is logged and the response is unaffected.
- **Responses written by an exception handler are recorded**, also when the handler is placed before this middleware.
- **The response is never buffered.** It is copied while it is written, up to `MaxBodyBytes`.
- `Enabled = false` turns everything off without removing the middleware.
- **In integration tests**, wait for the entry before the test ends (or turn the auditing off): a save that lands after the test dropped its database recreates it.

## Options

| Option | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Turns the auditing on or off. |
| `MaxBodyBytes` | `32768` | Largest body stored. Must be between 1 and `MaxBodyBytesLimit` (1048576). |
| `SaveTimeout` | `00:00:05` | Longest wait for the store; the save is then cancelled and logged. Must be positive. |
| `ClientIpHeader` | `null` | Header holding the caller address behind a proxy. Stored as received: set it only when the proxy overwrites the header. |
| `SensitiveHeaders` | see above | Headers masked. |
| `SensitiveFields` | see above | Fields masked. |
| `SensitiveNameParts` | see above | Parts of a field or header name that make it masked. |
| `CallerClaims` | `sub`, `client_id`, `azp` | Claim types copied into `Caller`. |

## Retention

Entries are never expired automatically. Old ones are deleted by an explicit purge, the same approach as SimpleHistory: `IRequestAuditPurger` (implemented by the storage provider) and its command-line front end, `PurgeRequestAuditCommand`.

```csharp
// In a console command of your app, e.g. "purge-request-audit"
return await PurgeRequestAuditCommand.ExecuteAsync(
    args, () => new MongoRequestAuditPurger(database, options), Console.Out);
```

| Argument | Meaning |
|---|---|
| *(none)* | Deletes the entries older than 365 days. |
| `--older-than-days=<N>` | Deletes the entries older than N days. Cannot be combined with `--from` / `--to`. |
| `--from=<date>` | Inclusive lower bound on `CreatedAtUtc` (`yyyy-MM-dd` or `yyyy-MM-ddTHH:mm:ssZ`). |
| `--to=<date>` | Exclusive upper bound (same formats). When only `--from` is given, the purge stops at the time it started. |
| `--dry-run` | Only counts what would be deleted. |

Dates accept a `Z` or an offset (`2026-02-01T03:00:00-03:00`) and neither bound may be in the future. The arguments are validated before the purger is created, so bad input never opens a connection. Deletion runs in batches (5000 entries per round trip by default).

## Your own store

Implement `IRequestAuditStore` and register it as a singleton:

```csharp
public sealed class MyStore : IRequestAuditStore
{
    public Task SaveAsync(RequestAuditEntry entry, CancellationToken cancellationToken = default) { ... }
}

services.AddSingleton<IRequestAuditStore, MyStore>();
```

For retention, also implement `IRequestAuditPurger`: delete the entries with `FromUtc <= CreatedAtUtc < ToUtc`, honouring the dry run and the batch size, and report the upper bound applied.
