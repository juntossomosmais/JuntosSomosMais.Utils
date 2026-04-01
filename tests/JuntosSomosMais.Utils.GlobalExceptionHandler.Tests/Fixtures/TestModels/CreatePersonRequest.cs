using System.ComponentModel.DataAnnotations;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;

public class CreatePersonRequest
{
    [Required]
    public string? Name { get; set; }

    [Required]
    public string? Email { get; set; }
}
