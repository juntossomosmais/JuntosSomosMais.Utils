using Microsoft.AspNetCore.Builder;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;

// The configuration the README recommends: keep the middleware diagnostics (log, HandledException event and
// error.type metric tag) even when a handler returns true.
public class KeepDiagnosticsWebAppFactory : LoggingWebAppFactory
{
    protected override void ConfigureExceptionHandlerOptions(ExceptionHandlerOptions options)
    {
        options.SuppressDiagnosticsCallback = _ => false;
    }
}
