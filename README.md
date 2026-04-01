# JuntosSomosMais.Utils.GlobalExceptionHandler

ASP.NET Core 8+ global exception handler built on the native `IExceptionHandler` infrastructure. Catches unhandled exceptions and returns a consistent, user-friendly JSON response with a request ID for traceability.

## Requirements

- .NET 8 or later
- ASP.NET Core 8 or later

## Installation

```bash
dotnet add package JuntosSomosMais.Utils.GlobalExceptionHandler
```

## Quick start

Register the handler in `Program.cs`:

```csharp
builder.Services.AddCustomExceptionHandler();

// ...

app.UseExceptionHandler();
```

That's it. All unhandled exceptions now return a structured 500 response with a friendly message and a request ID.

## Response format

Every unhandled exception produces the same shape:

```json
{
  "type": "UNEXPECTED_ERROR",
  "statusCode": 500,
  "error": {
    "requestId": "0HN4ABC123:00000001",
    "msg": "Oh, sorry! We didn't expect that 😬 Please inform the ID \"0HN4ABC123:00000001\" so we can help you properly."
  }
}
```

- `requestId` is the ASP.NET Core `HttpContext.TraceIdentifier`, useful for correlating with server-side logs.
- `msg` is a user-friendly message that embeds the request ID. The raw exception message is **never** exposed to the client.

## Mapping exceptions to HTTP status codes

Use the `[ExceptionStatusCode]` attribute on your own exception classes to map them to specific HTTP status codes:

```csharp
using JuntosSomosMais.Utils.GlobalExceptionHandler;
using Microsoft.AspNetCore.Http;

[ExceptionStatusCode(StatusCodes.Status404NotFound)]
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

[ExceptionStatusCode(StatusCodes.Status400BadRequest)]
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
```

When a `NotFoundException` is thrown, the handler returns HTTP 404 instead of 500:

```json
{
  "type": "NOT_FOUND_ERROR",
  "statusCode": 404,
  "error": {
    "requestId": "0HN4ABC123:00000001",
    "msg": "Oh, sorry! We didn't expect that 😬 Please inform the ID \"0HN4ABC123:00000001\" so we can help you properly."
  }
}
```

The `msg` field always contains the friendly message with the request ID, regardless of the status code. The original exception message is only visible when `ViewStackTrace` is enabled (see [Options](#options)).

### Default exception type labels

When you don't provide an explicit `ExceptionType`, the handler infers one from the status code:

| Status code | Default `type` label |
|---|---|
| 400 | `VALIDATION_ERRORS` |
| 401 | `UNAUTHORIZED_ERROR` |
| 403 | `FORBIDDEN_ERROR` |
| 404 | `NOT_FOUND_ERROR` |
| 503 | `SERVICE_UNAVAILABLE_ERROR` |
| Any other | `UNEXPECTED_ERROR` |

To override the default label, set `ExceptionType` explicitly:

```csharp
[ExceptionStatusCode(StatusCodes.Status400BadRequest, ExceptionType = "INVALID_INPUT")]
public class InvalidInputException : Exception
{
    public InvalidInputException(string message) : base(message) { }
}
```

### Attribute inheritance

The attribute uses `Inherited = true`, so subclasses automatically inherit the parent's mapping:

```csharp
[ExceptionStatusCode(StatusCodes.Status400BadRequest)]
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

// Inherits the 400 status code from DomainException — no attribute needed
public class InvalidStateException : DomainException
{
    public InvalidStateException(string message) : base(message) { }
}
```

### Migrating from `custom-exception-middleware`

If you are migrating from the `CustomExceptionMiddleware` NuGet package, replace the library's exception classes with your own annotated classes:

| Before (library class) | After (your class with attribute) |
|---|---|
| `throw new DomainException("msg")` | `throw new DomainException("msg")` — define your own with `[ExceptionStatusCode(400)]` |
| `throw new NotFoundException("msg")` | `throw new NotFoundException("msg")` — define your own with `[ExceptionStatusCode(404)]` |
| `throw new CannotAccessException("msg")` | `throw new ForbiddenException("msg")` — define your own with `[ExceptionStatusCode(403)]` |
| `throw new UnauthorizedException("msg")` | `throw new UnauthorizedException("msg")` — define your own with `[ExceptionStatusCode(401)]` |

**Step-by-step:**

1. Remove the `CustomExceptionMiddleware` package.
2. Install `JuntosSomosMais.Utils.GlobalExceptionHandler`.
3. Create your own exception classes with the `[ExceptionStatusCode]` attribute (see examples above).
4. Replace `app.UseCustomExceptionMiddleware()` with:
   ```csharp
   builder.Services.AddCustomExceptionHandler();
   // ...
   app.UseExceptionHandler();
   ```
5. Update your `throw` statements to use your new exception classes.

## Options

Pass a configuration delegate to `AddCustomExceptionHandler` to customise behaviour:

```csharp
builder.Services.AddCustomExceptionHandler(options =>
{
    // Show full exception details — recommended for Development only
    options.ViewStackTrace = builder.Environment.IsDevelopment();

    // Replace the default JSON serializer options
    options.JsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // Fully replace the response body
    options.CustomizeResponse = ctx => new
    {
        traceId = ctx.HttpContext.TraceIdentifier,
        message = ctx.Exception.Message,
        statusCode = ctx.StatusCode,
        errorType = ctx.ExceptionType
    };
});
```

### `ViewStackTrace`

`bool`, default `false`. When `true`, the response includes `error.detail` containing the full exception output (`exception.ToString()`). Enable only in non-production environments.

```json
{
  "type": "NOT_FOUND_ERROR",
  "statusCode": 404,
  "error": {
    "requestId": "0HN4ABC123:00000001",
    "msg": "Oh, sorry! We didn't expect that 😬 Please inform the ID \"0HN4ABC123:00000001\" so we can help you properly.",
    "detail": "MyApp.NotFoundException: Product not found\n   at MyApp.Controllers.ProductsController.GetAsync() ..."
  }
}
```

### `JsonSerializerOptions`

`JsonSerializerOptions?`, default `null`. Provide a custom instance to control serialization. When `null`, the handler uses camelCase naming, `UnsafeRelaxedJsonEscaping`, and indented output. The instance is made read-only by the handler for thread safety.

### `CustomizeResponse`

`Func<CustomExceptionContext, object>?`, default `null`. Override the entire response body. The delegate receives a `CustomExceptionContext` and must return any object; the handler serializes it using `JsonSerializerOptions`.

`CustomExceptionContext` properties:

| Property | Type | Description |
|----------|------|-------------|
| `HttpContext` | `HttpContext` | The current request context |
| `Exception` | `Exception` | The unhandled exception |
| `StatusCode` | `int` | The resolved HTTP status code (from the attribute or 500) |
| `ExceptionType` | `string` | The resolved type label (from the attribute, default mapping, or `UNEXPECTED_ERROR`) |

## Excluding endpoints from the handler

Apply `[IgnoreCustomException]` to a controller or action to let the exception bubble past the handler (e.g., to a fallback pipeline):

```csharp
// Entire controller
[IgnoreCustomException]
public class WebhookController : ControllerBase { ... }

// Single action
[HttpPost("internal")]
[IgnoreCustomException]
public IActionResult Internal() { ... }
```

When the attribute is present, `TryHandleAsync` returns `false` and the framework continues to the next registered exception handler.

## Logging

Every handled exception is logged at `Error` level via `ILogger<CustomExceptionHandler>`. The log message includes:

- `TraceId` — the request trace identifier
- `Message` — the exception message
- The full exception object (stack trace included regardless of `ViewStackTrace`)

## Full example

```csharp
// Program.cs
using JuntosSomosMais.Utils.GlobalExceptionHandler;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddCustomExceptionHandler(options =>
{
    options.ViewStackTrace = builder.Environment.IsDevelopment();
});

var app = builder.Build();

app.UseExceptionHandler();
app.MapControllers();
app.Run();
```

```csharp
// Exceptions/NotFoundException.cs
using JuntosSomosMais.Utils.GlobalExceptionHandler;
using Microsoft.AspNetCore.Http;

[ExceptionStatusCode(StatusCodes.Status404NotFound)]
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
```

```csharp
// Controllers/ProductsController.cs
[ApiController]
[Route("products")]
public class ProductsController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAsync(Guid id, [FromServices] AppDbContext db)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
            throw new NotFoundException($"Product {id} not found");

        return Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CreateProductRequest request, [FromServices] AppDbContext db)
    {
        // If this throws, the handler catches it and returns 500
        // with a friendly message + requestId for support traceability.
        var product = new Product { Name = request.Name };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAsync), new { id = product.Id }, product);
    }
}
```
