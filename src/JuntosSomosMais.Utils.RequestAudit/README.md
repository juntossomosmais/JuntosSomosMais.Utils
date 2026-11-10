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
| Headers in `SensitiveHeaders` | Value replaced by `***`. Default: `Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie`, `X-Api-Key`. |
| JSON properties (any depth), form fields and query parameters in `SensitiveFields` | Value replaced by `***`. Default: `password`, `secret`, `client_secret`, `token`, `access_token`, `refresh_token`, `cpf`, `email`, `phone`. |

Names are compared ignoring case. Add your own to either set.

A body is stored only when it can be masked: JSON (`application/json`, `+json`) and `application/x-www-form-urlencoded` (stored as a JSON object). It is **not** stored, and the `...BodyNote` field says why, when:

- the content type is anything else (multipart, plain text, binary);
- the JSON is invalid;
- it is larger than `MaxBodyBytes` (default 32 KB), because a cut body cannot be parsed and masked.

## Behavior to know

- **The entry is saved after the response is sent** (`Response.OnCompleted`), so the caller does not wait for the store.
- **Auditing never breaks the request.** If the store fails, the error is logged and the response is unaffected.
- **Responses written by an exception handler are recorded**, also when the handler is placed before this middleware.
- **The response is never buffered.** It is copied while it is written, up to `MaxBodyBytes`.
- `Enabled = false` turns everything off without removing the middleware.
- **In integration tests**, wait for the entry before the test ends (or turn the auditing off): a save that lands after the test dropped its database recreates it.

## Options

| Option | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Turns the auditing on or off. |
| `MaxBodyBytes` | `32768` | Largest body stored. |
| `ClientIpHeader` | `null` | Header holding the caller address behind a proxy. |
| `SensitiveHeaders` | see above | Headers masked. |
| `SensitiveFields` | see above | Fields masked. |
| `CallerClaims` | `sub`, `client_id`, `azp` | Claim types copied into `Caller`. |

## Your own store

Implement `IRequestAuditStore` and register it as a singleton:

```csharp
public sealed class MyStore : IRequestAuditStore
{
    public Task SaveAsync(RequestAuditEntry entry, CancellationToken cancellationToken = default) { ... }
}

services.AddSingleton<IRequestAuditStore, MyStore>();
```
