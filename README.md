# JuntosSomosMais.Utils

Opinionated configurations and utilities for .NET projects at Juntos Somos Mais.

## Packages

| Package | Description |
|---|---|
| [JuntosSomosMais.Utils.GlobalExceptionHandler](https://www.nuget.org/packages/JuntosSomosMais.Utils.GlobalExceptionHandler/) | ASP.NET Core 8+ global exception handler built on the native `IExceptionHandler` infrastructure. |

Install via CLI:

```bash
dotnet add package JuntosSomosMais.Utils.GlobalExceptionHandler
```

## JuntosSomosMais.Utils.GlobalExceptionHandler

Catches unhandled exceptions and returns a consistent, user-friendly JSON response with a request ID for traceability. Also standardizes validation error responses from FluentValidation and ASP.NET model-state validation.

### Requirements

- .NET 8 or later
- ASP.NET Core 8 or later

The package includes `FluentValidation` (v11.\*) as a transitive dependency. If your project uses a newer version (e.g., v12.x), keep your direct `FluentValidation` package reference -- NuGet will resolve to the higher version automatically without conflicts.

### Quick start

Register the handler in `Program.cs`:

```csharp
builder.Services.AddCustomExceptionHandler();
builder.Services.AddProblemDetails();

// ...

app.UseExceptionHandler();
```

> **Important:** `AddProblemDetails()` is required. ASP.NET Core 8's `ExceptionHandlerMiddlewareImpl` needs `IProblemDetailsService` registered when no `ExceptionHandlingPath` or `ExceptionHandler` delegate is set. Without it, `UseExceptionHandler()` throws `InvalidOperationException` at startup.

A single call to `AddCustomExceptionHandler()` configures:

1. **Exception handling** via `IExceptionHandler` -- catches unhandled exceptions and returns structured JSON.
2. **Validation error responses** -- overrides `InvalidModelStateResponseFactory` so that model-state errors (from data annotations, FluentValidation auto-validation, or model binding) return the same structured format.
3. **FluentValidation exception handling** -- catches `FluentValidation.ValidationException` (thrown by `ValidateAndThrowAsync`) and returns field-level errors.

### Response formats

#### Exception responses

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

#### Validation error responses

Model-state errors (from `[Required]`, `[Range]`, FluentValidation auto-validation, etc.) and `FluentValidation.ValidationException` both produce:

```json
{
  "type": "VALIDATION_ERRORS",
  "statusCode": 400,
  "error": {
    "Name": ["The field [Name] cannot be empty or null"],
    "Email": ["The field [Email] must be a valid email address"]
  }
}
```

- `type` is always `"VALIDATION_ERRORS"`.
- `error` is a dictionary mapping field names to arrays of error messages.
- This format is consistent regardless of whether the error came from model-state validation (before the controller action) or from `ValidateAndThrowAsync` (during the action).

### Mapping exceptions to HTTP status codes

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

#### Default exception type labels

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

#### Attribute inheritance

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

### Validation handling

#### How it works

`AddCustomExceptionHandler()` configures two validation interception points:

1. **`InvalidModelStateResponseFactory`** -- intercepts model-state validation failures before the controller action runs. This covers data annotations (`[Required]`, `[Range]`, etc.), FluentValidation auto-validation (if configured by the consumer), and model binding errors (invalid JSON, type mismatches).

2. **`FluentValidation.ValidationException`** -- caught by the `IExceptionHandler` when `ValidateAndThrowAsync()` is used inside a controller action.

Both paths produce the same `{ type, statusCode, error }` shape.

#### Using FluentValidation with manual validation (recommended)

FluentValidation now recommends [manual validation](https://docs.fluentvalidation.net/en/latest/aspnet.html). Register your validators and inject them into controllers:

```csharp
// Program.cs
builder.Services.AddScoped<IValidator<CreatePersonRequest>, CreatePersonRequestValidator>();
builder.Services.AddCustomExceptionHandler();
builder.Services.AddProblemDetails();

app.UseExceptionHandler();
```

```csharp
// Controller — option A: check result manually
[HttpPost]
public async Task<IActionResult> CreateAsync(
    CreatePersonRequest request,
    [FromServices] IValidator<CreatePersonRequest> validator)
{
    var result = await validator.ValidateAsync(request);
    if (!result.IsValid)
    {
        // Throw a domain exception or return your own response.
        // If you throw, the IExceptionHandler catches it.
        throw new DomainException(result.Errors.First().ErrorMessage);
    }
    // ...
}

// Controller — option B: use ValidateAndThrowAsync
[HttpPost]
public async Task<IActionResult> CreateAsync(
    CreatePersonRequest request,
    [FromServices] IValidator<CreatePersonRequest> validator)
{
    await validator.ValidateAndThrowAsync(request);
    // If validation fails, ValidationException is thrown.
    // The handler catches it and returns { type, statusCode, error } with field-level details.
    // ...
}
```

#### Migrating a custom `ModelValidationActionFilter`

If your project has a global action filter that resolves `IValidator<T>` from DI and returns `BadRequestObjectResult(new ValidationProblemDetails(...))`, you must change it to **throw** `FluentValidation.ValidationException` instead. Otherwise the filter bypasses the library and returns a different response shape.

**Before** (returns `ValidationProblemDetails` directly):

```csharp
if (!context.ModelState.IsValid)
{
    context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
    return;
}
```

**After** (throws `ValidationException` so the library catches it):

```csharp
var failures = new List<FluentValidation.Results.ValidationFailure>();

foreach (var argument in context.ActionArguments.Values)
{
    if (argument is null) continue;

    var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
    if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
        continue;

    var validationContext = new ValidationContext<object>(argument);
    var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

    if (!result.IsValid)
        failures.AddRange(result.Errors);
}

if (failures.Count > 0)
    throw new ValidationException(failures);
```

The library's `IExceptionHandler` catches the `ValidationException` and returns `{ type: "VALIDATION_ERRORS", statusCode: 400, error: { field: ["messages"] } }`.

> **Tip:** Pass `context.HttpContext.RequestAborted` as the `CancellationToken` to `ValidateAsync`. This ensures validators that hit the database (e.g., `MustAsync`) stop early when the client disconnects.

#### Important: `InvalidModelStateResponseFactory` override

`AddCustomExceptionHandler()` overrides `ApiBehaviorOptions.InvalidModelStateResponseFactory` to standardize validation responses. If your application has a custom `InvalidModelStateResponseFactory` (e.g., a `MvcBuilderExtension.ConfigureInvalidModelStateResponse()`), you should **remove it** after adopting this library -- the library now handles it for you.

#### Migrating from `custom-exception-middleware`

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
4. Remove any custom `MvcBuilderExtension.ConfigureInvalidModelStateResponse()` -- the library handles it.
5. Replace `app.UseCustomExceptionMiddleware()` with:
   ```csharp
   builder.Services.AddCustomExceptionHandler();
   builder.Services.AddProblemDetails();
   // ...
   app.UseExceptionHandler();
   ```
6. Update your `throw` statements to use your new exception classes.

### Options

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

#### `ViewStackTrace`

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

#### `JsonSerializerOptions`

`JsonSerializerOptions?`, default `null`. Provide a custom instance to control serialization. When `null`, the handler uses camelCase naming, `UnsafeRelaxedJsonEscaping`, and indented output. The instance is made read-only by the handler for thread safety. These options are used consistently for both exception responses and validation error responses.

#### `CustomizeResponse`

`Func<CustomExceptionContext, object>?`, default `null`. Override the entire response body. The delegate receives a `CustomExceptionContext` and must return any object; the handler serializes it using `JsonSerializerOptions`. This applies to both regular exception responses and `FluentValidation.ValidationException` responses.

`CustomExceptionContext` properties:

| Property | Type | Description |
|----------|------|-------------|
| `HttpContext` | `HttpContext` | The current request context |
| `Exception` | `Exception` | The unhandled exception |
| `StatusCode` | `int` | The resolved HTTP status code (from the attribute or 500) |
| `ExceptionType` | `string` | The resolved type label (from the attribute, default mapping, or `UNEXPECTED_ERROR`) |

### Excluding endpoints from the handler

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

### Logging

Every handled exception is logged at `Error` level via `ILogger<CustomExceptionHandler>`. The log message includes:

- `TraceId` — the request trace identifier
- `Message` — the exception message
- The full exception object (stack trace included regardless of `ViewStackTrace`)

### Full example

```csharp
// Program.cs
using JuntosSomosMais.Utils.GlobalExceptionHandler;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<IValidator<CreatePersonRequest>, CreatePersonRequestValidator>();
builder.Services.AddCustomExceptionHandler(options =>
{
    options.ViewStackTrace = builder.Environment.IsDevelopment();
});
builder.Services.AddProblemDetails();

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
    private readonly IValidator<CreateProductRequest> _validator;

    public ProductsController(IValidator<CreateProductRequest> validator)
    {
        _validator = validator;
    }

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
        // ValidateAndThrowAsync throws ValidationException if invalid.
        // The handler catches it and returns 400 with field-level errors.
        await _validator.ValidateAndThrowAsync(request);

        var product = new Product { Name = request.Name };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAsync), new { id = product.Id }, product);
    }
}
```
