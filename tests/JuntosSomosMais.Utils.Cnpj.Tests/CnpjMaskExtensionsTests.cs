using Xunit;

namespace JuntosSomosMais.Utils.Cnpj.Tests;

public class CnpjMaskExtensionsTests
{
    [Fact(DisplayName = "Should return null when input is null")]
    public void StripCnpjMask_NullInput_ReturnsNull()
    {
        // Arrange
        string cnpj = null!;

        // Act
        var result = cnpj.StripCnpjMask();

        // Assert
        Assert.Null(result);
    }

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

    [Fact(DisplayName = "Should return null when input is null")]
    public void NormalizeCnpj_NullInput_ReturnsNull()
    {
        // Arrange
        string cnpj = null!;

        // Act
        var result = cnpj.NormalizeCnpj();

        // Assert
        Assert.Null(result);
    }

    [Theory(DisplayName = "Should not pad empty, whitespace-only or mask-only input into a fake all-zeros CNPJ")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../-")]
    public void NormalizeCnpj_EmptyOrMaskOnlyInput_ReturnsEmptyStringNotAllZeros(string cnpj)
    {
        // Arrange & Act
        var result = cnpj.NormalizeCnpj();

        // Assert - the cleaned value stays empty and is never padded into "00000000000000"
        Assert.Equal(string.Empty, result);
        Assert.NotEqual("00000000000000", result);
    }

    [Fact(DisplayName = "Should pad a single lost leading zero")]
    public void NormalizeCnpj_OneZeroLost_ReturnsPaddedValue()
    {
        // Arrange
        var cnpj = "7000001000185";

        // Act
        var result = cnpj.NormalizeCnpj();

        // Assert
        Assert.Equal("07000001000185", result);
    }

    [Fact(DisplayName = "Should pad two lost leading zeros")]
    public void NormalizeCnpj_TwoZerosLost_ReturnsPaddedValue()
    {
        // Arrange
        var cnpj = "123456000149";

        // Act
        var result = cnpj.NormalizeCnpj();

        // Assert
        Assert.Equal("00123456000149", result);
    }

    [Fact(DisplayName = "Should strip the mask before padding a short masked value")]
    public void NormalizeCnpj_MaskedShortValue_StripsThenPads()
    {
        // Arrange
        var cnpj = "7.000.001/0001-85";

        // Act
        var result = cnpj.NormalizeCnpj();

        // Assert
        Assert.Equal("07000001000185", result);
    }

    [Fact(DisplayName = "Should pad any all-numeric value shorter than 14, with no plausibility window")]
    public void NormalizeCnpj_SingleDigit_PadsToFourteenCharacters()
    {
        // Arrange
        var cnpj = "1";

        // Act
        var result = cnpj.NormalizeCnpj();

        // Assert
        Assert.Equal("00000000000001", result);
    }

    [Fact(DisplayName = "Should leave a 14-character numeric value unchanged")]
    public void NormalizeCnpj_FourteenCharacterNumericValue_ReturnsUnchanged()
    {
        // Arrange
        var cnpj = "11222333000181";

        // Act
        var result = cnpj.NormalizeCnpj();

        // Assert
        Assert.Equal("11222333000181", result);
    }

    [Fact(DisplayName = "Should leave a 15-character value unchanged and never truncate")]
    public void NormalizeCnpj_FifteenCharacterValue_ReturnsUnchanged()
    {
        // Arrange
        var cnpj = "112223330001810";

        // Act
        var result = cnpj.NormalizeCnpj();

        // Assert
        Assert.Equal("112223330001810", result);
    }

    [Fact(DisplayName = "Should leave a short alphanumeric value unchanged and never pad it")]
    public void NormalizeCnpj_ShortAlphanumericValue_ReturnsUnchanged()
    {
        // Arrange
        var cnpj = "L20T2TJN0001";

        // Act
        var result = cnpj.NormalizeCnpj();

        // Assert
        Assert.Equal("L20T2TJN0001", result);
    }

    [Fact(DisplayName = "Should uppercase an already-14-character alphanumeric value")]
    public void NormalizeCnpj_LowercaseAlphanumericFourteenCharacters_ReturnsUppercased()
    {
        // Arrange
        var cnpj = "l20t2tjn000118";

        // Act
        var result = cnpj.NormalizeCnpj();

        // Assert
        Assert.Equal("L20T2TJN000118", result);
    }

    [Fact(DisplayName = "Should be idempotent when a padded value is normalized again")]
    public void NormalizeCnpj_AppliedTwiceToPaddedValue_ReturnsSameValue()
    {
        // Arrange - a short value, so the first call actually pads instead of exiting at the length guard
        var cnpj = "7000001000185";

        // Act
        var once = cnpj.NormalizeCnpj();
        var twice = once.NormalizeCnpj();

        // Assert - re-normalizing a padded value neither pads again nor truncates
        Assert.Equal("07000001000185", once);
        Assert.Equal(once, twice);
    }

    [Fact(DisplayName = "Should leave an already-canonical value unchanged")]
    public void NormalizeCnpj_AlreadyCanonicalValue_ReturnsSameValue()
    {
        // Arrange
        var cnpj = "07000001000185";

        // Act
        var result = cnpj.NormalizeCnpj();

        // Assert
        Assert.Equal("07000001000185", result);
    }

    [Fact(DisplayName = "Should pad an all-zeros fragment into a value Validate still rejects")]
    public void NormalizeCnpj_AllZerosFragment_PadsButDoesNotValidate()
    {
        // Arrange - non-empty and all-digits, so unlike "" this one legitimately pads
        var cnpj = "000";

        // Act
        var result = cnpj.NormalizeCnpj();

        // Assert - IsValidFormat accepts the padded form, so Validate's all-zeros guard is the only thing rejecting it
        Assert.Equal("00000000000000", result);
        Assert.True(CnpjValidator.IsValidFormat(result));
        Assert.False(CnpjValidator.Validate(result));
    }

    [Fact(DisplayName = "Should pad the reconstruction-hazard example into a genuinely valid CNPJ")]
    public void NormalizeCnpj_ReconstructionHazardValue_PadsIntoValidCnpj()
    {
        // Arrange - a real CNPJ whose leading zero was eaten; padding cannot tell this from an unrelated typo
        var cnpj = "3456700010065";

        // Act
        var result = cnpj.NormalizeCnpj();

        // Assert - the pad succeeds and the result validates, which is exactly the documented hazard
        Assert.Equal("03456700010065", result);
        Assert.True(CnpjValidator.Validate(result));
    }

    [Fact(DisplayName = "Should strip the mask from a value that is already 14 characters")]
    public void NormalizeCnpj_MaskedFourteenCharacterValue_StripsWithoutPadding()
    {
        // Arrange
        var cnpj = "11.222.333/0001-81";

        // Act
        var result = cnpj.NormalizeCnpj();

        // Assert
        Assert.Equal("11222333000181", result);
    }
}
