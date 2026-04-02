using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace JuntosSomosMais.Utils.Instrumentation.Tests;

public class ModelValidationActionFilterTests
{
    public class SampleDto
    {
        public string? Name { get; set; }
    }

    public class AnotherSampleDto
    {
        public string? Email { get; set; }
    }

    private static ActionExecutingContext CreateContext(
        IServiceProvider serviceProvider,
        Dictionary<string, object?> actionArguments)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            actionArguments!,
            controller: null!);
    }

    [Fact(DisplayName = "Should call next when there are no action arguments")]
    public async Task OnActionExecutionAsync_WithNoArguments_ShouldCallNext()
    {
        // Arrange
        var serviceProviderMock = new Mock<IServiceProvider>();
        var context = CreateContext(serviceProviderMock.Object, new Dictionary<string, object?>());
        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor),
                new List<IFilterMetadata>(),
                controller: null!));
        };
        var filter = new ModelValidationActionFilter();

        // Act
        await filter.OnActionExecutionAsync(context, next);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact(DisplayName = "Should call next when argument is null")]
    public async Task OnActionExecutionAsync_WithNullArgument_ShouldCallNext()
    {
        // Arrange
        var serviceProviderMock = new Mock<IServiceProvider>();
        var context = CreateContext(serviceProviderMock.Object, new Dictionary<string, object?>
        {
            ["dto"] = null
        });
        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor),
                new List<IFilterMetadata>(),
                controller: null!));
        };
        var filter = new ModelValidationActionFilter();

        // Act
        await filter.OnActionExecutionAsync(context, next);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact(DisplayName = "Should call next when no validator is registered for the argument type")]
    public async Task OnActionExecutionAsync_WithNoRegisteredValidator_ShouldCallNext()
    {
        // Arrange
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IValidator<SampleDto>)))
            .Returns(null!);

        var context = CreateContext(serviceProviderMock.Object, new Dictionary<string, object?>
        {
            ["dto"] = new SampleDto { Name = "Test" }
        });
        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor),
                new List<IFilterMetadata>(),
                controller: null!));
        };
        var filter = new ModelValidationActionFilter();

        // Act
        await filter.OnActionExecutionAsync(context, next);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact(DisplayName = "Should call next when argument is valid")]
    public async Task OnActionExecutionAsync_WithValidArgument_ShouldCallNext()
    {
        // Arrange
        var validatorMock = new Mock<IValidator<SampleDto>>();
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IValidator<SampleDto>)))
            .Returns(validatorMock.Object);

        var context = CreateContext(serviceProviderMock.Object, new Dictionary<string, object?>
        {
            ["dto"] = new SampleDto { Name = "Valid" }
        });
        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor),
                new List<IFilterMetadata>(),
                controller: null!));
        };
        var filter = new ModelValidationActionFilter();

        // Act
        await filter.OnActionExecutionAsync(context, next);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact(DisplayName = "Should throw ValidationException when argument is invalid")]
    public async Task OnActionExecutionAsync_WithInvalidArgument_ShouldThrowValidationException()
    {
        // Arrange
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required")
        };
        var validatorMock = new Mock<IValidator<SampleDto>>();
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IValidator<SampleDto>)))
            .Returns(validatorMock.Object);

        var context = CreateContext(serviceProviderMock.Object, new Dictionary<string, object?>
        {
            ["dto"] = new SampleDto()
        });
        ActionExecutionDelegate next = () => Task.FromResult(new ActionExecutedContext(
            new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor),
            new List<IFilterMetadata>(),
            controller: null!));
        var filter = new ModelValidationActionFilter();

        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => filter.OnActionExecutionAsync(context, next));

        // Assert
        var error = Assert.Single(exception.Errors);
        Assert.Equal("Name", error.PropertyName);
        Assert.Equal("Name is required", error.ErrorMessage);
    }

    [Fact(DisplayName = "Should throw ValidationException with all failures from multiple invalid arguments")]
    public async Task OnActionExecutionAsync_WithMultipleInvalidArguments_ShouldThrowValidationExceptionWithAllFailures()
    {
        // Arrange
        var sampleFailures = new List<ValidationFailure>
        {
            new("Name", "Name is required")
        };
        var sampleValidatorMock = new Mock<IValidator<SampleDto>>();
        sampleValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(sampleFailures));

        var anotherFailures = new List<ValidationFailure>
        {
            new("Email", "Email is required")
        };
        var anotherValidatorMock = new Mock<IValidator<AnotherSampleDto>>();
        anotherValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(anotherFailures));

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IValidator<SampleDto>)))
            .Returns(sampleValidatorMock.Object);
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IValidator<AnotherSampleDto>)))
            .Returns(anotherValidatorMock.Object);

        var context = CreateContext(serviceProviderMock.Object, new Dictionary<string, object?>
        {
            ["sample"] = new SampleDto(),
            ["another"] = new AnotherSampleDto()
        });
        ActionExecutionDelegate next = () => Task.FromResult(new ActionExecutedContext(
            new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor),
            new List<IFilterMetadata>(),
            controller: null!));
        var filter = new ModelValidationActionFilter();

        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => filter.OnActionExecutionAsync(context, next));

        // Assert
        Assert.Equal(2, exception.Errors.Count());
        Assert.Contains(exception.Errors, e => e.PropertyName == "Name" && e.ErrorMessage == "Name is required");
        Assert.Contains(exception.Errors, e => e.PropertyName == "Email" && e.ErrorMessage == "Email is required");
    }

    [Fact(DisplayName = "Should throw ArgumentNullException when MvcOptions is null")]
    public void AddFluentValidationAPIFilter_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        MvcOptions? options = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => options!.AddFluentValidationAPIFilter());
    }
}
