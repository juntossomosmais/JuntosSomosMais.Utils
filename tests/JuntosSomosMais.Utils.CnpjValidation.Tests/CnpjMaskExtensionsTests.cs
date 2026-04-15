using Xunit;

namespace JuntosSomosMais.Utils.CnpjValidation.Tests;

public class CnpjMaskExtensionsTests
{
    [Fact(DisplayName = "Should strip dots, slash and dash from masked numeric CNPJ")]
    public void StripCnpjMask_MaskedNumericCnpj_ReturnsUnmasked()
    {
        // Arrange
        var cnpj = "11.222.333/0001-81";

        // Act
        var result = cnpj.StripCnpjMask();

        // Assert
        Assert.Equal("11222333000181", result);
    }

    [Fact(DisplayName = "Should strip dots, slash and dash from masked alphanumeric CNPJ")]
    public void StripCnpjMask_MaskedAlphanumericCnpj_ReturnsUnmasked()
    {
        // Arrange
        var cnpj = "AB.123.C4D/0001-92";

        // Act
        var result = cnpj.StripCnpjMask();

        // Assert
        Assert.Equal("AB123C4D000192", result);
    }

    [Fact(DisplayName = "Should uppercase lowercase letters")]
    public void StripCnpjMask_LowercaseInput_ReturnsUppercase()
    {
        // Arrange
        var cnpj = "ab.123.c4d/0001-92";

        // Act
        var result = cnpj.StripCnpjMask();

        // Assert
        Assert.Equal("AB123C4D000192", result);
    }

    [Fact(DisplayName = "Should return unmasked string unchanged when no mask characters are present")]
    public void StripCnpjMask_UnmaskedInput_ReturnsSameValue()
    {
        // Arrange
        var cnpj = "11222333000181";

        // Act
        var result = cnpj.StripCnpjMask();

        // Assert
        Assert.Equal("11222333000181", result);
    }
}
