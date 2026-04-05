using FluentValidation;
using JuntosSomosMais.Ziggurat;
using Microsoft.Extensions.Logging;

namespace JuntosSomosMais.Utils.Instrumentation;

public class MessageValidationMiddleware<TMessage> : IConsumerMiddleware<TMessage> where TMessage : IMessage
{
    private readonly ILogger<MessageValidationMiddleware<TMessage>> _logger;
    private readonly IServiceProvider _serviceProvider;

    public MessageValidationMiddleware(
        ILogger<MessageValidationMiddleware<TMessage>> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task OnExecutingAsync(TMessage message, ConsumerServiceDelegate<TMessage> next, CancellationToken cancellationToken)
    {
        var validator = _serviceProvider.GetService(typeof(IValidator<TMessage>)) as IValidator<TMessage> ?? throw new InvalidOperationException(
                $"No IValidator<{typeof(TMessage).Name}> is registered. A validator is required when using {nameof(MessageValidationMiddleware<TMessage>)}.");

        var result = await validator.ValidateAsync(message, cancellationToken);

        if (!result.IsValid)
        {
            _logger.LogWarning("Message validation error: {MessageGroup}. Errors: {Errors}",
                message.MessageGroup, string.Join(", ", result.Errors));
            return;
        }

        await next(message, cancellationToken);
    }
}
