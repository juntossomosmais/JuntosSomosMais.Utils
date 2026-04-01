using FluentValidation;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;
using Microsoft.AspNetCore.Mvc;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Controllers;

[ApiController]
[Route("person")]
public class PersonController : ControllerBase
{
    private readonly IValidator<CreatePersonRequest> _validator;

    public PersonController(IValidator<CreatePersonRequest> validator)
    {
        _validator = validator;
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreatePersonRequest request)
    {
        return Ok(request);
    }

    [HttpPost("validate-and-throw")]
    public async Task<IActionResult> CreateWithValidateAndThrowAsync([FromBody] CreatePersonRequest request)
    {
        await _validator.ValidateAndThrowAsync(request);
        return Ok(request);
    }
}
