using Microsoft.AspNetCore.Mvc;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Controllers;

[IgnoreCustomException]
[ApiController]
[Route("values")]
public class ValuesController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        throw new Exception("Some error ignore class");
    }
}
