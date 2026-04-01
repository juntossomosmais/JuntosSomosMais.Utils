using FluentValidation;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestValidators;

public class CreatePersonRequestValidator : AbstractValidator<CreatePersonRequest>
{
    public CreatePersonRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("The field [Name] cannot be empty or null");
        RuleFor(x => x.Email).NotEmpty().WithMessage("The field [Email] cannot be empty or null")
            .EmailAddress().WithMessage("The field [Email] must be a valid email address");
    }
}
