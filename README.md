# JuntosSomosMais.Utils.GlobalExceptionHandler

ASP.NET Core 8+ global exception handler built on the native `IExceptionHandler` infrastructure. Catches unhandled exceptions across your application and converts them into consistent, structured JSON responses.

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

That's it. All unhandled exceptions are now caught and serialized to JSON.

## Response format

Every error response follows the same shape:

```json
{
  "type": "VALIDATION_ERRORS",
  "error": {
    "msg": "Product not found."
  }
}
```

When `ViewStackTrace` is enabled (see [options](#options)), `error` gains a `detail` field:

```json
{
  "type": "UNEXPECTED_ERROR",
  "error": {
    "msg": "Object reference not set to an instance of an object.",
    "detail": "   at MyApp.Services.ProductService.GetAsync() ..."
  }
}
```

The `type` field identifies the error category (see [exception type resolution](#exception-type-resolution)).

## Built-in exception types

The library ships four concrete exception classes. Throw them from anywhere in your application and the handler maps them to the correct HTTP status code automatically.

| Exception | HTTP status |
|-----------|-------------|
| `DomainException` (abstract, any subclass) | 400 Bad Request |
| `UnauthorizedException` | 401 Unauthorized |
| `CannotAccessException` | 403 Forbidden |
| `NotFoundException` | 404 Not Found |

Any other exception type that is **not** mapped → `500 Internal Server Error`.

### Constructor signatures

All concrete exceptions share the same constructor set:

```csharp
// Message only
throw new NotFoundException("Product not found.");

// Message + custom type label (sets the "type" field in the response)
throw new NotFoundException("Product not found.", "PRODUCT_NOT_FOUND");

// Message + inner exception
throw new NotFoundException("Product not found.", innerException);

// Message + custom type label + inner exception
throw new NotFoundException("Product not found.", "PRODUCT_NOT_FOUND", innerException);
```

## Exception type resolution

The `type` field in the response is resolved as follows:

| Condition | Value |
|-----------|-------|
| `CustomException` subclass with `ExceptionType` set | the value of `ExceptionType` |
| `CustomException` subclass without `ExceptionType` | `"VALIDATION_ERRORS"` |
| Any other exception | `"UNEXPECTED_ERROR"` |

## Defining your own exceptions

### Subclassing a built-in type

Derive from any of the built-in exceptions to get their status code mapping for free:

```csharp
public class OrderNotFoundException : NotFoundException
{
    public OrderNotFoundException(Guid orderId)
        : base($"Order '{orderId}' was not found.", "ORDER_NOT_FOUND") { }
}
```

### Subclassing `DomainException`

`DomainException` is the base for validation/business-rule errors (400). Derive from it to create your own domain exceptions:

```csharp
public class InvalidCouponException : DomainException
{
    public InvalidCouponException(string code)
        : base($"Coupon '{code}' is expired or does not exist.", "INVALID_COUPON") { }
}
```

### Subclassing `CustomException` directly

Use `CustomException` as the base when you want to control the HTTP status code through `ExceptionMappings` (see [mapping custom exceptions](#mapping-custom-exceptions-to-status-codes)):

```csharp
public abstract class ConflictException : CustomException
{
    protected ConflictException(string message, string exceptionType)
        : base(message) { ExceptionType = exceptionType; }
}

public class DuplicateEmailException : ConflictException
{
    public DuplicateEmailException(string email)
        : base($"Email '{email}' is already registered.", "DUPLICATE_EMAIL") { }
}
```

## Options

Pass a configuration delegate to `AddCustomExceptionHandler` to customise behaviour:

```csharp
builder.Services.AddCustomExceptionHandler(options =>
{
    // Show stack traces — recommended for Development only
    options.ViewStackTrace = builder.Environment.IsDevelopment();

    // Map additional exception types to HTTP status codes
    options.ExceptionMappings[typeof(ConflictException)] = HttpStatusCode.Conflict;

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
        status = (int)ctx.StatusCode,
        code = ctx.ExceptionType,
        message = ctx.Exception.Message
    };
});
```

### `ViewStackTrace`

`bool`, default `false`. When `true`, the response body uses `CustomErrorDetailResponse` which adds an `error.detail` field containing the exception's stack trace. Enable only in non-production environments.

### `ExceptionMappings`

`Dictionary<Type, HttpStatusCode>`. Register additional exception types and their HTTP status codes.

Consumer mappings are checked **before** built-in ones, so they can override defaults. If you register a **base class**, all subclasses that are not explicitly registered will also match:

```csharp
options.ExceptionMappings[typeof(ConflictException)] = HttpStatusCode.Conflict;
// DuplicateEmailException (which extends ConflictException) → 409
```

Resolution order:
1. Exact type match in `ExceptionMappings`
2. Exact type match in built-in mappings (`Unauthorized`, `Forbidden`, `NotFound`)
3. Base-class match in `ExceptionMappings`
4. Assignable to `DomainException` → 400
5. Everything else → 500

### `JsonSerializerOptions`

`JsonSerializerOptions?`, default `null`. Provide a custom instance to control serialization. When `null`, the handler uses camelCase naming, `UnsafeRelaxedJsonEscaping`, and indented output.

### `CustomizeResponse`

`Func<CustomExceptionContext, object>?`, default `null`. Override the entire response body. The delegate receives a `CustomExceptionContext` and must return any object; the handler serializes it using `JsonSerializerOptions`.

`CustomExceptionContext` properties:

| Property | Type | Description |
|----------|------|-------------|
| `HttpContext` | `HttpContext` | The current request context |
| `Exception` | `Exception` | The unhandled exception |
| `StatusCode` | `HttpStatusCode` | The resolved HTTP status code |
| `ExceptionType` | `string` | The resolved type label |

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
- `ExceptionType` — the resolved type label
- `Message` — the exception message
- The full exception object (stack trace included regardless of `ViewStackTrace`)

Configure the log level for this class through your logging configuration:

```json
{
  "Logging": {
    "LogLevel": {
      "JuntosSomosMais.Utils.GlobalExceptionHandler.CustomExceptionHandler": "Error"
    }
  }
}
```

## Full example

```csharp
// Program.cs
using System.Net;
using JuntosSomosMais.Utils.GlobalExceptionHandler;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddCustomExceptionHandler(options =>
{
    options.ViewStackTrace = builder.Environment.IsDevelopment();
    options.ExceptionMappings[typeof(ConflictException)] = HttpStatusCode.Conflict;
});

var app = builder.Build();

app.UseExceptionHandler();
app.MapControllers();
app.Run();
```

```csharp
// Domain exceptions
public class ProductNotFoundException : NotFoundException
{
    public ProductNotFoundException(Guid id)
        : base($"Product '{id}' was not found.", "PRODUCT_NOT_FOUND") { }
}

public abstract class ConflictException : CustomException
{
    protected ConflictException(string message, string exceptionType)
        : base(message) { ExceptionType = exceptionType; }
}

public class DuplicateSkuException : ConflictException
{
    public DuplicateSkuException(string sku)
        : base($"SKU '{sku}' already exists.", "DUPLICATE_SKU") { }
}
```

```csharp
// Controller
[ApiController]
[Route("products")]
public class ProductsController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult Get(Guid id)
    {
        throw new ProductNotFoundException(id);
        // → 404 { "type": "PRODUCT_NOT_FOUND", "error": { "msg": "Product '...' was not found." } }
    }

    [HttpPost]
    public IActionResult Create(CreateProductRequest request)
    {
        throw new DuplicateSkuException(request.Sku);
        // → 409 { "type": "DUPLICATE_SKU", "error": { "msg": "SKU '...' already exists." } }
    }
}
```
