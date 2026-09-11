using Xunit;

namespace JuntosSomosMais.Utils.Cnpj.Tests;

public class CnpjValidatorTests
{
    [Fact(DisplayName = "Should return false when CNPJ is null")]
    public void Validate_NullCnpj_ReturnsFalse()
    {
        // Arrange
        string cnpj = null!;

        // Act
        var result = CnpjValidator.Validate(cnpj);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "Should return false when CNPJ is empty")]
    public void Validate_EmptyCnpj_ReturnsFalse()
    {
        // Arrange
        var cnpj = string.Empty;

        // Act
        var result = CnpjValidator.Validate(cnpj);

        // Assert
        Assert.False(result);
    }

    [Theory(DisplayName = "Should return false when CNPJ length is wrong after stripping mask")]
    [InlineData("1122233300018")]      // 13 chars — too short
    [InlineData("112223330001810")]    // 15 chars — too long
    public void Validate_WrongLength_ReturnsFalse(string cnpj)
    {
        // Arrange & Act
        var result = CnpjValidator.Validate(cnpj);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "Should return false when CNPJ is all zeros")]
    public void Validate_AllZerosCnpj_ReturnsFalse()
    {
        // Arrange
        var cnpj = "00000000000000";

        // Act
        var result = CnpjValidator.Validate(cnpj);

        // Assert
        Assert.False(result);
    }

    [Theory(DisplayName = "Should return false when CNPJ body contains invalid characters")]
    [InlineData("1122233!000181")]   // special char in body
    [InlineData("11222333000A8A")]   // letter in check-digit area
    public void Validate_InvalidCharacters_ReturnsFalse(string cnpj)
    {
        // Arrange & Act
        var result = CnpjValidator.Validate(cnpj);

        // Assert
        Assert.False(result);
    }

    [Theory(DisplayName = "Should return false when check digits are wrong")]
    [InlineData("11222333000182")]   // second digit should be 1, not 2
    [InlineData("11222333000190")]   // first digit should be 8, not 9
    [InlineData("12345678000196")]   // second digit should be 5, not 6
    public void Validate_InvalidCheckDigits_ReturnsFalse(string cnpj)
    {
        // Arrange & Act
        var result = CnpjValidator.Validate(cnpj);

        // Assert
        Assert.False(result);
    }

    [Theory(DisplayName = "Should return true for valid numeric CNPJs")]
    [InlineData("11222333000181")]
    [InlineData("12345678000195")]
    public void Validate_ValidNumericCnpj_ReturnsTrue(string cnpj)
    {
        // Arrange & Act
        var result = CnpjValidator.Validate(cnpj);

        // Assert
        Assert.True(result);
    }

    [Theory(DisplayName = "Should return true for valid masked numeric CNPJs")]
    [InlineData("11.222.333/0001-81")]
    [InlineData("12.345.678/0001-95")]
    public void Validate_ValidMaskedNumericCnpj_ReturnsTrue(string cnpj)
    {
        // Arrange & Act
        var result = CnpjValidator.Validate(cnpj);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "Should return true for valid alphanumeric CNPJ")]
    public void Validate_ValidAlphanumericCnpj_ReturnsTrue()
    {
        // Arrange
        var cnpj = "L20T2TJN000118";

        // Act
        var result = CnpjValidator.Validate(cnpj);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "Should return true for valid masked alphanumeric CNPJ")]
    public void Validate_ValidMaskedAlphanumericCnpj_ReturnsTrue()
    {
        // Arrange
        var cnpj = "L2.0T2.TJN/0001-18";

        // Act
        var result = CnpjValidator.Validate(cnpj);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "Should return true for valid lowercase alphanumeric CNPJ")]
    public void Validate_ValidLowercaseAlphanumericCnpj_ReturnsTrue()
    {
        // Arrange
        var cnpj = "L20T2tjn000118";

        // Act
        var result = CnpjValidator.Validate(cnpj);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "Should return false for alphanumeric CNPJ with wrong check digits")]
    public void Validate_AlphanumericCnpjWithWrongCheckDigits_ReturnsFalse()
    {
        // Arrange
        var cnpj = "L2.0T2.TJN/0001-15";

        // Act
        var result = CnpjValidator.Validate(cnpj);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "Should return false when CNPJ is null")]
    public void IsValidFormat_NullCnpj_ReturnsFalse()
    {
        // Arrange
        string cnpj = null!;

        // Act
        var result = CnpjValidator.IsValidFormat(cnpj);

        // Assert
        Assert.False(result);
    }

    [Theory(DisplayName = "Should return true for valid numeric CNPJ formats")]
    [InlineData("11222333000181")]
    [InlineData("11.222.333/0001-81")]
    public void IsValidFormat_ValidNumericFormat_ReturnsTrue(string cnpj)
    {
        // Arrange & Act
        var result = CnpjValidator.IsValidFormat(cnpj);

        // Assert
        Assert.True(result);
    }

    [Theory(DisplayName = "Should return true for valid alphanumeric CNPJ formats")]
    [InlineData("AB123C4D000192")]
    [InlineData("AB.123.C4D/0001-92")]
    [InlineData("ab.123.c4d/0001-92")]
    public void IsValidFormat_ValidAlphanumericFormat_ReturnsTrue(string cnpj)
    {
        // Arrange & Act
        var result = CnpjValidator.IsValidFormat(cnpj);

        // Assert
        Assert.True(result);
    }

    [Theory(DisplayName = "Should return false for invalid CNPJ formats")]
    [InlineData("")]
    [InlineData("1122233300018")]
    [InlineData("112223330001810")]
    [InlineData("11.222.333/0001-8")]
    [InlineData("11.222.333/0001-AB")]
    public void IsValidFormat_InvalidFormat_ReturnsFalse(string cnpj)
    {
        // Arrange & Act
        var result = CnpjValidator.IsValidFormat(cnpj);

        // Assert
        Assert.False(result);
    }

    [Theory(DisplayName = "Should return false for structurally invalid inputs")]
    [InlineData("11.222.3.33/0001-81")]
    [InlineData("11_222_333/0001-81")]
    [InlineData("11 222 333 0001 81")]
    [InlineData("CNPJ 11222333000181")]
    public void Validate_StructurallyInvalidInput_ReturnsFalse(string cnpj)
    {
        // Arrange & Act
        var result = CnpjValidator.Validate(cnpj);

        // Assert
        Assert.False(result);
    }

    [Fact(DisplayName = "Should validate a zero-padded CNPJ while Validate itself stays unpadded")]
    public void Validate_NormalizedCnpjWithLostLeadingZero_ReturnsTrueWhileRawInputStaysFalse()
    {
        // Arrange
        var cnpj = "7000001000185";

        // Act
        var normalizedResult = CnpjValidator.Validate(cnpj.NormalizeCnpj());
        var rawResult = CnpjValidator.Validate(cnpj);

        // Assert - the pad recovers a validatable CNPJ, and Validate itself did not start padding
        Assert.True(normalizedResult);
        Assert.False(rawResult);
    }

    [Fact(DisplayName = "Should widen acceptance for loosely-spaced input via NormalizeCnpj while Validate alone keeps rejecting it")]
    public void Validate_NormalizedLooselySpacedCnpj_ReturnsTrueWhileRawInputStaysFalse()
    {
        // Arrange
        var cnpj = "11 222 333 0001 81";

        // Act
        var normalized = cnpj.NormalizeCnpj();
        var normalizedResult = CnpjValidator.Validate(normalized);
        var rawResult = CnpjValidator.Validate(cnpj);

        // Assert - this is a deliberate behavior delta: NormalizeCnpj recovers the value, Validate alone must not
        Assert.Equal("11222333000181", normalized);
        Assert.True(normalizedResult);
        Assert.False(rawResult);
    }
}
