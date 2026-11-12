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

Masking is by **name**: the key of a JSON property, form field, query parameter or header decides whether its value is replaced by `***`. Values themselves are never inspected, so a CPF sent under a key the lists do not know (`"obs": "cpf 123..."`) is stored as received; add that key to the lists, or turn the body off for that service (see [Bodies](#bodies)).

| What | Rule |
|---|---|
| Headers in `SensitiveHeaders` | Exact name, ignoring case. Default: `Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie`, `X-Api-Key`, `X-Auth-Token`, `X-Access-Token`, `Api-Key`, `Location`, `Content-Location` (an OAuth callback redirect carries `code` or `id_token`; this also hides the resource URL of a `201 Created`, so remove them if you need it). |
| Fields in `SensitiveFields` (**exact**) | The normalized name equals the term, or its plural (`s`/`es`). Default: `cpf`, `cnpj`, `cpfcnpj`, `rg`, `document`, `documento`, `inscricaoestadual`, `email`, `phone`, `telephone`, `telefone`, `celular`, `mobile`, `whatsapp`, `address`, `endereco`, `logradouro`, `street`, `postalcode`, `zipcode`, `cep`, `district`, `bairro`, `birthdate`, `birthday`, `datanascimento`, `cardnumber`, `cvv`, `cvc`, `authorization`. |
| Fields and headers containing one of `SensitiveNameParts` (**contains**) | The normalized name contains the term anywhere. Default: `password`, `senha`, `secret`, `token`, `apikey`, `cpf`, `cnpj`, `email`, `document`, `phone`, `telefone`, `celular`, `cardnumber`, `birth`, `nascimento`. |

Field names, and header names checked against `SensitiveNameParts`, are compared on a normalized form, on both sides: each named segment of the key (split on `.`, `[` and `]`, numeric indexes skipped), without `_` and `-`, ignoring case. A key is masked when any of its segments matches, so `card_number`, `cardNumber` and `Payment.CardNumber` are the same name, `customer.phone.number` and `customer[phone][number]` are masked through `phone`, and a JSON key that contains dots (`"email.verified"`) is judged per segment too:

- **exact** (`SensitiveFields`): `email` masks `email`, `Email`, `emails`, `filter.email`; `address` masks `addresses`;
- **contains** (`SensitiveNameParts`): `token` masks `access_token`, `idToken`, `X-CSRF-Token`; `document` masks `customerDocument`; `email` masks `userEmail`; `phone` masks `phoneNumber` and `mobilePhone`.

**Exact-only terms.** Short or common terms are in `SensitiveFields` only, because as parts they would mask unrelated names: `rg` (`orgId`, `cargo`), `cep` (`accepted`, `exception`), `address` (`ipAddress`), `street` (`streetNumber`), `district`, `cvv`. `ipAddress` is therefore stored, and so is a field like `streetNumber`.

**Where it applies.** JSON properties at any depth; a sensitive key holding an object or array is masked whole (`"address": {...}` becomes `"address": "***"`). A string value holding JSON (common in webhooks, `"payload": "{\"cpf\":\"...\"}"`) is parsed, masked by the same names and stored back as a string, up to 3 levels of JSON inside strings; a string that is not valid JSON is kept as received. After 100 strings in one body that look like JSON but are not, later strings starting with `{` or `[` are stored as `***`.

**Not masked by default:** `name` and `nome` (names of companies and people are often needed to read an audit), `city`, `state`, order and product fields. Add them if your service must not store them.

**Adding and removing names.** The three sets are public and mutable:

```csharp
services.AddRequestAudit(options =>
{
    options.SensitiveFields.Add("name");           // exact: name, names, customer.name
    options.SensitiveFields.Add("nome");
    options.SensitiveNameParts.Add("pix");         // contains: pixKey, chave_pix
    options.SensitiveHeaders.Add("Referer");

    // A term present in both sets (cpf, cnpj, email, document, phone, telefone, celular, cardnumber)
    // must be removed from both to stop masking it.
    options.SensitiveFields.Remove("email");
    options.SensitiveNameParts.Remove("email");
});
```

Not masked:

- the URL path (only the query string goes through the field rules), so keep secrets out of route values;
- the `Referer` header, which can carry another page's query string; add it to `SensitiveHeaders` if that matters.

`CallerClaims` matches claim types as they are on the `ClaimsPrincipal`. The exception is `sub`: when the principal has no `sub` (JwtBearer's default `MapInboundClaims = true` renames it to `ClaimTypes.NameIdentifier`), that claim is read instead and still stored as `sub`, whatever the authentication scheme. A real `sub` wins.

## Bodies

Request and response bodies are stored by default (`StoreRequestBody`, `StoreResponseBody`), always masked. Evaluate per service whether they are needed: when one is off, it is not read (request) or copied (response), and the `...BodyNote` field says `Not stored: StoreRequestBody is off.` (or `StoreResponseBody`). Method, URL, query, status and headers are always stored, masked.

```csharp
services.AddRequestAudit(options => options.StoreResponseBody = false);
```

A body is stored only when it can be masked: JSON (`application/json`, `+json`) and `application/x-www-form-urlencoded` (stored as a JSON object). It is **not** stored, and the `...BodyNote` field says why, when:

- the content type is anything else (multipart, plain text, binary);
- the JSON is invalid;
- it is larger than `MaxBodyBytes` (default 32 KB, at most 1 MB), because a cut body cannot be parsed and masked.

## Behavior to know

- **The entry is saved after the response is sent** (`Response.OnCompleted`), so the caller does not wait for the store. On HTTP/1.1, the server reads the next request on the same connection only after the save, so a slow store can delay that request, but never longer than `SaveTimeout` (default 5 s): the save is then cancelled and the failure logged, even when the store ignores the cancellation.
- **Auditing never breaks the request.** If the store fails, the error is logged and the response is unaffected.
- **Responses written by an exception handler are recorded**, also when the handler is placed before this middleware.
- **A request that cannot have a body (a GET without one) or whose content type cannot be masked is not read**, so it costs no buffer.
- **The response is never buffered.** It is copied while it is written, up to `MaxBodyBytes`.
- **The request body is never buffered beyond `MaxBodyBytes` + 1 bytes**, with or without `Content-Length`, and the endpoint reads the same bytes (a failed read is replayed to it too). A body that fits in `MaxBodyBytes` reaches the endpoint as a seekable stream; a larger one does not, so call `EnableBuffering()` before rewinding it, as without the audit.
- `Enabled = false` turns everything off without removing the middleware.
- **In integration tests**, wait for the entry before the test ends (or turn the auditing off): a save that lands after the test dropped its database recreates it.

## Options

| Option | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Turns the auditing on or off. |
| `MaxBodyBytes` | `32768` | Largest body stored. Must be between 1 and `MaxBodyBytesLimit` (1048576). |
| `SaveTimeout` | `00:00:05` | Longest wait for the store; the save is then cancelled and logged. Must be positive. |
| `StoreRequestBody` | `true` | Stores the request body, masked. When off it is not read. |
| `StoreResponseBody` | `true` | Stores the response body, masked. When off it is not copied. |
| `ClientIpHeader` | `null` | Header holding the caller address behind a proxy. Stored as received: set it only when the proxy overwrites the header. |
| `SensitiveHeaders` | see above | Headers masked. |
| `SensitiveFields` | see above | Field names masked on exact match (and plural). |
| `SensitiveNameParts` | see above | Parts of a field or header name that make it masked (contains). |
| `CallerClaims` | `sub`, `client_id`, `azp` | Claim types copied into `Caller`. |

## Retention

**Nothing expires by itself, and scheduling the purge is mandatory.** There is no TTL: entries stay until they are deleted by an explicit purge (the same approach as SimpleHistory) through `IRequestAuditPurger` (implemented by the storage provider) and its command-line front end, `PurgeRequestAuditCommand`. A service that adopts the package without scheduling the purge keeps personal data forever.

**Agree the retention with Product and Legal.** Audit entries hold personal data (masked, but the caller IP, URLs and unmasked fields remain). Under the LGPD, data may be kept only as long as necessary for its purpose (necessity principle, art. 6, III; end of processing and deletion, arts. 15 and 16). The default is 30 days; use a longer window only when a documented purpose (a contract, a dispute window, a regulation) requires it.

```csharp
// In a console command of your app, e.g. "purge-request-audit"
return await PurgeRequestAuditCommand.ExecuteAsync(
    args, () => new MongoRequestAuditPurger(database, options), Console.Out);
```

| Argument | Meaning |
|---|---|
| *(none)* | Deletes the entries older than 30 days (`PurgeRequestAuditArgs.DefaultOlderThanDays`). |
| `--older-than-days=<N>` | Deletes the entries older than N days. Cannot be combined with `--from` / `--to`. |
| `--from=<date>` | Inclusive lower bound on `CreatedAtUtc` (`yyyy-MM-dd` or `yyyy-MM-ddTHH:mm:ssZ`). |
| `--to=<date>` | Exclusive upper bound (same formats). When only `--from` is given, the purge stops at the time it started. |
| `--dry-run` | Only counts what would be deleted. |

Dates accept a `Z` or an offset (`2026-02-01T03:00:00-03:00`) and neither bound may be in the future. The arguments are validated before the purger is created, so bad input never opens a connection. Deletion runs in batches (5000 entries per round trip by default).

Schedule it daily, for example with a Kubernetes CronJob (pass the window explicitly, so it is visible where it runs):

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: my-api-purge-request-audit
spec:
  schedule: "0 6 * * *"            # daily, 03:00 in Sao Paulo
  concurrencyPolicy: Forbid        # never two purges at once
  successfulJobsHistoryLimit: 1
  failedJobsHistoryLimit: 3
  jobTemplate:
    spec:
      backoffLimit: 2
      template:
        spec:
          restartPolicy: Never
          containers:
            - name: purge
              image: my-registry/my-api:latest   # the same image as the app
              args: ["purge-request-audit", "--older-than-days=30"]
              envFrom:
                - secretRef:
                    name: my-api-secrets         # the database connection
```

**Backups extend the real retention.** A purged entry still lives in the database snapshots (for example MongoDB Atlas backups) until each snapshot expires, so the effective retention is the purge window plus the snapshot retention. Take that into account in the agreement with Product and Legal.

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
