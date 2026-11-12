namespace JuntosSomosMais.Utils.RequestAudit;

/// <summary>Options of the request auditing.</summary>
public sealed class RequestAuditOptions
{
    /// <summary>Upper bound accepted for <see cref="MaxBodyBytes"/>.</summary>
    public const int MaxBodyBytesLimit = 1024 * 1024;

    /// <summary>Turns the auditing off without removing it from the pipeline.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Largest body stored, at most <see cref="MaxBodyBytesLimit"/>. A larger body is not stored.</summary>
    public int MaxBodyBytes { get; set; } = 32 * 1024;

    /// <summary>
    /// Stores the request body (masked). When off, the body is not read and the entry says it was not stored.
    /// </summary>
    public bool StoreRequestBody { get; set; } = true;

    /// <summary>
    /// Stores the response body (masked). When off, the response is not copied and the entry says it was not stored.
    /// </summary>
    public bool StoreResponseBody { get; set; } = true;

    /// <summary>
    /// Longest wait for the store to save an entry. The save then is cancelled and the failure logged.
    /// </summary>
    public TimeSpan SaveTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Header holding the address of the caller when the app is behind a proxy (for example <c>True-Client-IP</c>).
    /// The connection address is used when it is not set or the header is missing. The value is stored as received,
    /// so set it only when the proxy overwrites the header; otherwise any caller can choose the address recorded.
    /// </summary>
    public string? ClientIpHeader { get; set; }

    /// <summary>
    /// Headers whose value is masked, compared ignoring case. <c>Location</c> is in because an OAuth callback
    /// redirect carries <c>code</c> or <c>id_token</c> in it.
    /// </summary>
    public ISet<string> SensitiveHeaders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Proxy-Authorization", "Cookie", "Set-Cookie", "X-Api-Key", "X-Auth-Token", "X-Access-Token",
        "Api-Key", "Location", "Content-Location",
    };

    /// <summary>
    /// JSON properties (at any depth, also inside JSON held in a string value), form fields and query parameters
    /// whose value is masked. A key is masked when any of its named segments (not the numeric indexes) matches
    /// (<c>Input.Address</c>, <c>user[cep]</c>, <c>customer.phone.number</c>; a JSON key with dots is judged per
    /// segment too). Names are compared exactly, ignoring case, <c>_</c> and <c>-</c>, and also match their plural (<c>emails</c>,
    /// <c>addresses</c>). Short or ambiguous terms belong here, not in <see cref="SensitiveNameParts"/>: <c>rg</c> or
    /// <c>cep</c> as parts would mask <c>orgId</c> or <c>accepted</c>.
    /// </summary>
    public ISet<string> SensitiveFields { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "cpf", "cnpj", "cpfcnpj", "rg", "document", "documento", "inscricaoestadual",
        "email", "phone", "telephone", "telefone", "celular", "mobile", "whatsapp",
        "address", "endereco", "logradouro", "street", "postalcode", "zipcode", "cep", "district", "bairro",
        "birthdate", "birthday", "datanascimento",
        "cardnumber", "cvv", "cvc", "authorization",
    };

    /// <summary>
    /// Parts of a name that make any field or header containing them sensitive (<c>newPassword</c>,
    /// <c>id_token</c>, <c>X-Client-Secret</c>, <c>customerDocument</c>). Compared on the same normalized form as
    /// <see cref="SensitiveFields"/>.
    /// </summary>
    public ISet<string> SensitiveNameParts { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "password", "senha", "secret", "token", "apikey",
        "cpf", "cnpj", "email", "document", "phone", "telefone", "celular", "cardnumber", "birth", "nascimento",
    };

    /// <summary>
    /// Claim types copied from the authenticated user into the entry. <c>sub</c> falls back to the
    /// <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/> claim, where JwtBearer's default claim mapping
    /// puts it, and is stored as <c>sub</c>.
    /// </summary>
    public IList<string> CallerClaims { get; } = new List<string> { "sub", "client_id", "azp" };
}
