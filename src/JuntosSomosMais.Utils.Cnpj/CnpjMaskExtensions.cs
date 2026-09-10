namespace JuntosSomosMais.Utils.Cnpj;

public static class CnpjMaskExtensions
{
    public static string? StripCnpjMask(this string? cnpj)
    {
        if (cnpj == null)
            return null;
        Span<char> buf = stackalloc char[cnpj.Length];
        var n = 0;
        foreach (var c in cnpj)
            if (char.IsAsciiLetterOrDigit(c))
                buf[n++] = char.ToUpperInvariant(c);
        return new string(buf[..n]);
    }

    public static string? NormalizeCnpj(this string? cnpj)
    {
        if (cnpj == null)
            return null;

        var cleaned = cnpj.StripCnpjMask()!;

        if (cleaned.Length == 0 || cleaned.Length >= 14)
            return cleaned;

        var isAllDigits = true;
        foreach (var c in cleaned)
            if (!char.IsAsciiDigit(c))
            {
                isAllDigits = false;
                break;
            }

        if (!isAllDigits)
            return cleaned;

        return cleaned.PadLeft(14, '0');
    }
}
