namespace Vip.RestClient;

public class JwtGeneric
{
    #region Properties

    public string Issuer { get; set; }
    public long ExpirationTime { get; set; }
    public long IssuerAt { get; set; }
    public long NotBefore { get; set; }
    public string Subject { get; set; }
    public string Audience { get; set; }

    #endregion
}