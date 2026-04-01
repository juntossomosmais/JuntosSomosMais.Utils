using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestExceptions;
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

    [HttpGet("exception")]
    public IActionResult GetException([FromQuery] bool returnCustomer = false)
    {
        if (returnCustomer)
            return Ok(new[] { new Customer { Id = 1, Name = "Customer 1" } });
        throw new Exception("Custom exception message");
    }

    [HttpGet("not-found")]
    public IActionResult GetNotFound()
    {
        throw new NotFoundException("Customer not found");
    }

    [HttpGet("domain-error")]
    public IActionResult GetDomainError()
    {
        throw new DomainException("Invalid customer data");
    }

    [HttpGet("custom-typed")]
    public IActionResult GetCustomTyped()
    {
        throw new CustomTypedException("Service is down");
    }

    [HttpGet("child-not-found")]
    public IActionResult GetChildNotFound() => throw new ChildNotFoundException("Child entity not found");
}
