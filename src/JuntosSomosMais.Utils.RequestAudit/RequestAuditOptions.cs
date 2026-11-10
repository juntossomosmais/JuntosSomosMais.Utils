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
    /// Longest wait for the store to save an entry. The save then is cancelled and the failure logged.
    /// </summary>
    public TimeSpan SaveTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Header holding the address of the caller when the app is behind a proxy (for example <c>True-Client-IP</c>).
    /// The connection address is used when it is not set or the header is missing. The value is stored as received,
    /// so set it only when the proxy overwrites the header; otherwise any caller can choose the address recorded.
    /// </summary>
    public string? ClientIpHeader { get; set; }

    /// <summary>Headers whose value is masked, compared ignoring case.</summary>
    public ISet<string> SensitiveHeaders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Proxy-Authorization", "Cookie", "Set-Cookie", "X-Api-Key", "X-Auth-Token", "X-Access-Token",
        "Api-Key",
    };

    /// <summary>
    /// JSON properties (at any depth), form fields and query parameters whose value is masked. Names are compared
    /// on their last segment (<c>Input.Password</c>, <c>user[password]</c>), ignoring case, <c>_</c> and <c>-</c>,
    /// and also match their plural (<c>emails</c>).
    /// </summary>
    public ISet<string> SensitiveFields { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "cpf", "cnpj", "email", "phone", "telefone", "celular", "cvv", "authorization",
    };

    /// <summary>
    /// Parts of a name that make any field or header containing them sensitive (<c>newPassword</c>,
    /// <c>id_token</c>, <c>X-Client-Secret</c>). Compared on the same normalized form as <see cref="SensitiveFields"/>.
    /// </summary>
    public ISet<string> SensitiveNameParts { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "password", "senha", "secret", "token", "apikey",
    };

    /// <summary>Claim types copied from the authenticated user into the entry.</summary>
    public IList<string> CallerClaims { get; } = new List<string> { "sub", "client_id", "azp" };
}
