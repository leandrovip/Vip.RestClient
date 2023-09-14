using System;
using System.Text;

namespace Vip.RestClient;

public class JwtBase
{
    #region Constructors

    protected JwtBase() { }

    #endregion

    #region Properties

    public string OriginalToken { get; protected set; }
    public string Header { get; protected set; }
    public string Payload { get; protected set; }
    public byte[] Signature { get; protected set; }

    #endregion

    #region Static Methods

    public static JwtBase ParseText(string token)
    {
        if (string.IsNullOrEmpty(token)) throw new ArgumentException($"'{nameof(token)}' cannot be null or empty.", nameof(token));

        var parts = token.Split('.');
        if (parts.Length != 3) throw new ArgumentException("Token must have 3 sections");

        return new JwtBase
        {
            Header = GetBase64(parts[0]),
            Payload = GetBase64(parts[1]),
            Signature = ConvertFromBase64String(parts[2]),
            OriginalToken = token,
        };
    }

    private static string GetBase64(string base64)
    {
        var arr = ConvertFromBase64String(base64);
        return Encoding.UTF8.GetString(arr, 0, arr.Length);
    }

    private static byte[] ConvertFromBase64String(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        try
        {
            var working = input.Replace('-', '+').Replace('_', '/');
            while (working.Length % 4 != 0) working += '=';
            return Convert.FromBase64String(working);
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion
}