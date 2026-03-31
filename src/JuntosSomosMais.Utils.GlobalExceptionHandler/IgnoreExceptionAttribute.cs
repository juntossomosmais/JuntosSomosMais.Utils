namespace JuntosSomosMais.Utils.GlobalExceptionHandler;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IgnoreCustomExceptionAttribute : Attribute { }
