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
}
