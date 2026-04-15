using System.Text.RegularExpressions;

namespace JuntosSomosMais.Utils.CnpjValidation;

public static partial class CnpjValidator
{
    [GeneratedRegex(@"^[A-Z0-9]{2}\.?[A-Z0-9]{3}\.?[A-Z0-9]{3}\/?[A-Z0-9]{4}-?\d{2}$", RegexOptions.IgnoreCase)]
    private static partial Regex CnpjFormatRegex();

    private static readonly int[] _multi1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
    private static readonly int[] _multi2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

    public static bool Validate(string cnpj)
    {
        if (cnpj == null)
            return false;

        cnpj = cnpj.StripCnpjMask();

        if (cnpj.Length != 14 || cnpj.All(c => c == '0'))
            return false;

        if (!cnpj[..12].All(c => char.IsDigit(c) || (c >= 'A' && c <= 'Z')))
            return false;

        if (!cnpj[12..].All(char.IsDigit))
            return false;

        var tempCnpj = cnpj.Substring(0, 12);
        var sum = 0;
        for (int i = 0; i < 12; i++)
            sum += CharToValue(tempCnpj[i]) * _multi1[i];

        var rest = sum % 11;
        if (rest < 2)
            rest = 0;
        else
            rest = 11 - rest;

        var digit = rest.ToString();
        tempCnpj = tempCnpj + digit;
        sum = 0;

        for (int i = 0; i < 13; i++)
            sum += CharToValue(tempCnpj[i]) * _multi2[i];

        rest = sum % 11;

        if (rest < 2)
            rest = 0;
        else
            rest = 11 - rest;

        digit = digit + rest;

        return cnpj.EndsWith(digit);
    }

    public static bool IsValidFormat(string cnpj) => cnpj != null && CnpjFormatRegex().IsMatch(cnpj);

    private static int CharToValue(char c) => c - '0';
}
