using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;
using Microsoft.AspNetCore.Mvc;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Controllers;

[ApiController]
[Route("product")]
public class ProductController : ControllerBase
{
    [HttpGet]
    public IActionResult Get([FromQuery] int count = 5)
    {
        var products = Enumerable.Range(1, count)
            .Select(i => new Product { Id = i, Name = $"Product {i}" });
        return Ok(products);
    }

    [HttpGet("domain")]
    public IActionResult GetDomain([FromQuery] bool returnProduct = false)
    {
        if (returnProduct)
            return Ok(new[] { new Product { Id = 1, Name = "Product 1" } });
        throw new CustomDomainException("Custom domain exception message");
    }

    [HttpGet("custom-domain")]
    public IActionResult GetCustomDomain([FromQuery] bool returnProduct = false)
    {
        if (returnProduct)
            return Ok(new[] { new Product { Id = 1, Name = "Product 1" } });
        throw new CustomDomainException("Custom domain exception message", "OTHER_CUSTOM_TYPE");
    }

    [IgnoreCustomException]
    [HttpGet("ignore")]
    public IActionResult GetIgnore()
    {
        throw new Exception("Some error ignore method");
    }

    [HttpGet("conflict")]
    public IActionResult GetConflict()
    {
        throw new ConflictTestException("Conflict error message");
    }

    [HttpGet("base-class-mapping")]
    public IActionResult GetBaseClassMapping()
    {
        throw new ConcreteSubException("Base class mapping error");
    }
}
