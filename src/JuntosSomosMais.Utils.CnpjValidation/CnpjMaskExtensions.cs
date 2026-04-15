namespace JuntosSomosMais.Utils.CnpjValidation;

public static class CnpjMaskExtensions
{
    public static string StripCnpjMask(this string cnpj) =>
         cnpj == null ? null : string.Concat(cnpj.Where(c => char.IsDigit(c) || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))).ToUpperInvariant();
}
