using JuntosSomosMais.Ziggurat;

namespace JuntosSomosMais.Utils.Instrumentation;

public static class InstrumentationMiddlewareOptionsExtensions
{
    public static MiddlewareOptions<TMessage> UseFluentValidationConsumerMiddleware<TMessage>(
        this MiddlewareOptions<TMessage> options) where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Use<MessageValidationMiddleware<TMessage>>();
        return options;
    }
}
