using System.Net;
using System.Net.Http.Json;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;
using Microsoft.Extensions.Logging;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomExceptionHandlerLogLevelTests : IClassFixture<LoggingWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly FakeLogCollector _logCollector;
    private const string HandlerCategory = "JuntosSomosMais.Utils.GlobalExceptionHandler.CustomExceptionHandler";
    private const string MiddlewareCategory = "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";

    public CustomExceptionHandlerLogLevelTests(LoggingWebAppFactory factory)
    {
        _client = factory.CreateClient();
        _logCollector = factory.LogCollector;
        _logCollector.Clear();
    }

    [Fact(DisplayName = "Should log at Debug level when ValidationException is thrown")]
    public async Task TryHandleAsync_ValidationException_ShouldLogDebug()
    {
        // Arrange
        var request = new CreatePersonRequest { Name = "John", Email = "not-an-email" };

        // Act
        var response = await _client.PostAsJsonAsync("/person/validate-and-throw", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var entry = Assert.Single(_logCollector.Entries, e =>
            e.CategoryName == HandlerCategory && e.Message.Contains("validation exception"));
        Assert.Equal(LogLevel.Debug, entry.Level);
    }

    [Fact(DisplayName = "Should log at Debug level when attributed 404 exception is thrown")]
    public async Task TryHandleAsync_NotFoundException_ShouldLogDebug()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/customer/not-found");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var entry = Assert.Single(_logCollector.Entries, e =>
            e.CategoryName == HandlerCategory && e.Message.Contains("Customer not found"));
        Assert.Equal(LogLevel.Debug, entry.Level);
    }

    [Fact(DisplayName = "Should log at Debug level when attributed 400 exception is thrown")]
    public async Task TryHandleAsync_DomainException_ShouldLogDebug()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/customer/domain-error");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var entry = Assert.Single(_logCollector.Entries, e =>
            e.CategoryName == HandlerCategory && e.Message.Contains("Invalid customer data"));
        Assert.Equal(LogLevel.Debug, entry.Level);
    }

    [Fact(DisplayName = "Should log at Error level when unattributed 500 exception is thrown")]
    public async Task TryHandleAsync_UnattributedException_ShouldLogError()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/customer/exception");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var entry = Assert.Single(_logCollector.Entries, e =>
            e.CategoryName == HandlerCategory && e.Message.Contains("Custom exception message"));
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact(DisplayName = "Should log at Error level when attributed 503 exception is thrown")]
    public async Task TryHandleAsync_ServiceUnavailableException_ShouldLogError()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/customer/custom-typed");

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var entry = Assert.Single(_logCollector.Entries, e =>
            e.CategoryName == HandlerCategory && e.Message.Contains("Service is down"));
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    // The handler entry must be the same on .NET 8 and .NET 10; the framework middleware entry is what differs.
    [Fact(DisplayName = "Should write the handler entry and the framework-specific middleware entry for a handled 4xx")]
    public async Task TryHandleAsync_HandledNotFoundException_ShouldLogHandlerEntryOnEveryFramework()
    {
        // Arrange
        const string exceptionMessage = "Customer not found";

        // Act
        var response = await _client.GetAsync("/customer/not-found");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var handlerEntry = Assert.Single(_logCollector.Entries, e =>
            e.CategoryName == HandlerCategory && e.Message.Contains(exceptionMessage));
        Assert.Equal(LogLevel.Debug, handlerEntry.Level);
        Assert.Equal(exceptionMessage, handlerEntry.Exception?.Message);
        AssertMiddlewareEntryForHandledException(exceptionMessage);
    }

    [Fact(DisplayName = "Should write the handler Error entry and the framework-specific middleware entry for a handled 5xx")]
    public async Task TryHandleAsync_HandledUnattributedException_ShouldLogHandlerErrorOnEveryFramework()
    {
        // Arrange
        const string exceptionMessage = "Custom exception message";

        // Act
        var response = await _client.GetAsync("/customer/exception");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var handlerEntry = Assert.Single(_logCollector.Entries, e =>
            e.CategoryName == HandlerCategory && e.Message.Contains(exceptionMessage));
        Assert.Equal(LogLevel.Error, handlerEntry.Level);
        Assert.Equal(exceptionMessage, handlerEntry.Exception?.Message);
        AssertMiddlewareEntryForHandledException(exceptionMessage);
    }

    private void AssertMiddlewareEntryForHandledException(string exceptionMessage)
    {
        var middlewareEntries = _logCollector.Entries
            .Where(e => e.CategoryName == MiddlewareCategory && e.Exception?.Message == exceptionMessage)
            .ToList();

#if NET10_0_OR_GREATER
        // .NET 10 suppresses the middleware diagnostics when an IExceptionHandler returns true.
        Assert.Empty(middlewareEntries);
#else
        // .NET 8 logs the exception at Error before calling the handlers; services silence it with a Fatal override.
        var middlewareEntry = Assert.Single(middlewareEntries);
        Assert.Equal(LogLevel.Error, middlewareEntry.Level);
#endif
    }

#if NET10_0_OR_GREATER
    // With SuppressDiagnosticsCallback returning false, .NET 10 emits the middleware entry again, as .NET 8 does.
    public class WithDiagnosticsKept : IClassFixture<KeepDiagnosticsWebAppFactory>
    {
        private readonly HttpClient _client;
        private readonly FakeLogCollector _logCollector;

        public WithDiagnosticsKept(KeepDiagnosticsWebAppFactory factory)
        {
            _client = factory.CreateClient();
            _logCollector = factory.LogCollector;
            _logCollector.Clear();
        }

        [Fact(DisplayName = "Should write the middleware Error entry like .NET 8 when diagnostics are kept")]
        public async Task TryHandleAsync_HandledExceptionWithDiagnosticsKept_ShouldLogMiddlewareError()
        {
            // Arrange
            const string exceptionMessage = "Customer not found";

            // Act
            var response = await _client.GetAsync("/customer/not-found");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var handlerEntry = Assert.Single(_logCollector.Entries, e =>
                e.CategoryName == HandlerCategory && e.Message.Contains(exceptionMessage));
            Assert.Equal(LogLevel.Debug, handlerEntry.Level);
            var middlewareEntry = Assert.Single(_logCollector.Entries, e =>
                e.CategoryName == MiddlewareCategory && e.Exception?.Message == exceptionMessage);
            Assert.Equal(LogLevel.Error, middlewareEntry.Level);
        }
    }
#endif
}
