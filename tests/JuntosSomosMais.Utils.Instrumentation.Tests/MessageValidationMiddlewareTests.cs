using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Ziggurat;

namespace JuntosSomosMais.Utils.Instrumentation.Tests;

public class MessageValidationMiddlewareTests
{
    private readonly Mock<ILogger<MessageValidationMiddleware<TestMessage>>> _loggerMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();

    public class TestMessage : IMessage
    {
        public string MessageId { get; set; } = default!;
        public string MessageGroup { get; set; } = default!;
    }

    private MessageValidationMiddleware<TestMessage> CreateMiddleware()
    {
        return new MessageValidationMiddleware<TestMessage>(_loggerMock.Object, _serviceProviderMock.Object);
    }

    private static TestMessage CreateMessage(string messageId = "msg-1", string messageGroup = "test-group")
    {
        return new TestMessage
        {
            MessageId = messageId,
            MessageGroup = messageGroup
        };
    }

    [Fact(DisplayName = "Should call next when message is valid")]
    public async Task OnExecutingAsync_WithValidMessage_ShouldCallNext()
    {
        // Arrange
        var validatorMock = new Mock<IValidator<TestMessage>>();
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<TestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IValidator<TestMessage>)))
            .Returns(validatorMock.Object);

        var middleware = CreateMiddleware();
        var message = CreateMessage();
        var nextCalled = false;
        ConsumerServiceDelegate<TestMessage> next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnExecutingAsync(message, next);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact(DisplayName = "Should log warning and not call next when message is invalid")]
    public async Task OnExecutingAsync_WithInvalidMessage_ShouldLogWarningAndNotCallNext()
    {
        // Arrange
        var failures = new List<ValidationFailure>
        {
            new("MessageId", "MessageId is required")
        };
        var validatorMock = new Mock<IValidator<TestMessage>>();
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<TestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IValidator<TestMessage>)))
            .Returns(validatorMock.Object);

        var middleware = CreateMiddleware();
        var message = CreateMessage();
        var nextCalled = false;
        ConsumerServiceDelegate<TestMessage> next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await middleware.OnExecutingAsync(message, next);

        // Assert
        Assert.False(nextCalled);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Message validation error")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(DisplayName = "Should throw InvalidOperationException when no validator is registered")]
    public async Task OnExecutingAsync_WithNoValidatorRegistered_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IValidator<TestMessage>)))
            .Returns(null!);

        var middleware = CreateMiddleware();
        var message = CreateMessage();
        ConsumerServiceDelegate<TestMessage> next = _ => Task.CompletedTask;

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.OnExecutingAsync(message, next));

        // Assert
        Assert.Contains("TestMessage", exception.Message);
    }

    [Fact(DisplayName = "Should throw ArgumentNullException when MiddlewareOptions is null")]
    public void UseFluentValidationConsumerMiddleware_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        MiddlewareOptions<TestMessage>? options = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => options!.UseFluentValidationConsumerMiddleware());
    }
}
