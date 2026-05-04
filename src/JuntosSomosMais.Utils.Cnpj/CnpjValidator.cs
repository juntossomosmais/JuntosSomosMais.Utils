using System.Text.RegularExpressions;

namespace JuntosSomosMais.Utils.Cnpj;

public static partial class CnpjValidator
{
    [GeneratedRegex(@"^[A-Z0-9]{2}\.?[A-Z0-9]{3}\.?[A-Z0-9]{3}\/?[A-Z0-9]{4}-?\d{2}$", RegexOptions.IgnoreCase)]
    private static partial Regex CnpjFormatRegex();

    private static readonly int[] _multi1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] _multi2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    public static bool Validate(string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj) || !IsValidFormat(cnpj))
            return false;

        var stripped = cnpj.StripCnpjMask()!;

        if (stripped.All(c => c == '0'))
            return false;

        var firstDigit = CalculateDigit(_multi1, stripped.AsSpan()[..12]);

        if (stripped[12] - '0' != firstDigit)
            return false;

        var secondDigit = CalculateDigit(_multi2, stripped.AsSpan()[..13]);
        return stripped[13] - '0' == secondDigit;
    }

    private static int CalculateDigit(int[] weights, ReadOnlySpan<char> digits)
    {
        var sum = 0;
        for (var i = 0; i < weights.Length; i++)
            sum += weights[i] * (digits[i] - '0');

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    public static bool IsValidFormat(string? cnpj) => cnpj != null && CnpjFormatRegex().IsMatch(cnpj);
}
