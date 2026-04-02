using Microsoft.AspNetCore.Mvc;

namespace JuntosSomosMais.Utils.Instrumentation;

public static class InstrumentationMvcOptionsExtensions
{
    public static MvcOptions AddFluentValidationAPIFilter(this MvcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Filters.Add<ModelValidationActionFilter>();
        return options;
    }
}
