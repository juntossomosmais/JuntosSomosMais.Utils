using JuntosSomosMais.Utils.GlobalExceptionHandler.CustomExceptions;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;
using Microsoft.AspNetCore.Mvc;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Controllers;

[ApiController]
[Route("customer")]
public class CustomerController : ControllerBase
{
    [HttpGet]
    public IActionResult Get([FromQuery] int count = 5)
    {
        var customers = Enumerable.Range(1, count)
            .Select(i => new Customer { Id = i, Name = $"Customer {i}" });
        return Ok(customers);
    }

    [HttpGet("domain")]
    public IActionResult GetDomain([FromQuery] bool returnCustomer = false)
    {
        if (returnCustomer)
            return Ok(new[] { new Customer { Id = 1, Name = "Customer 1" } });
        throw new InvalidStateException("Custom domain exception message");
    }

    [HttpGet("unauthorized")]
    public IActionResult GetUnauthorized([FromQuery] bool returnCustomer = false)
    {
        if (returnCustomer)
            return Ok(new[] { new Customer { Id = 1, Name = "Customer 1" } });
        throw new UnauthorizedException("Custom unauthorized exception message");
    }

    [HttpGet("cannot-access")]
    public IActionResult GetCannotAccess([FromQuery] bool returnCustomer = false)
    {
        if (returnCustomer)
            return Ok(new[] { new Customer { Id = 1, Name = "Customer 1" } });
        throw new CannotAccessException("Custom cannot access exception message");
    }

    [HttpGet("not-found")]
    public IActionResult GetNotFound([FromQuery] bool returnCustomer = false)
    {
        if (returnCustomer)
            return Ok(new[] { new Customer { Id = 1, Name = "Customer 1" } });
        throw new NotFoundException("Custom not found exception message");
    }

    [HttpGet("exception")]
    public IActionResult GetException([FromQuery] bool returnCustomer = false)
    {
        if (returnCustomer)
            return Ok(new[] { new Customer { Id = 1, Name = "Customer 1" } });
        throw new Exception("Custom exception message");
    }
}
