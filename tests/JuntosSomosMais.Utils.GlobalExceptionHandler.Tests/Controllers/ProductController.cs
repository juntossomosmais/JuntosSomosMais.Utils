using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestExceptions;
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

    [HttpGet("exception")]
    public IActionResult GetException([FromQuery] bool returnProduct = false)
    {
        if (returnProduct)
            return Ok(new[] { new Product { Id = 1, Name = "Product 1" } });
        throw new InvalidOperationException("Something went wrong");
    }

    [IgnoreCustomException]
    [HttpGet("ignore")]
    public IActionResult GetIgnore()
    {
        throw new Exception("Some error ignore method");
    }

    [HttpGet("not-found")]
    public IActionResult GetNotFound()
    {
        throw new NotFoundException("Product not found");
    }

    [HttpGet("domain-error")]
    public IActionResult GetDomainError()
    {
        throw new DomainException("Invalid product data");
    }
}
