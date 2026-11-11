#if NET10_0_OR_GREATER
using Microsoft.AspNetCore.Builder;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;

// The configuration the README recommends to services moving to .NET 10: keep the middleware diagnostics (log,
// HandledException event and error.type metric tag) the way .NET 8 emits them, even when a handler returns true.
public class KeepDiagnosticsWebAppFactory : LoggingWebAppFactory
{
    protected override void ConfigureExceptionHandlerOptions(ExceptionHandlerOptions options)
    {
        options.SuppressDiagnosticsCallback = _ => false;
    }
}
#endif
