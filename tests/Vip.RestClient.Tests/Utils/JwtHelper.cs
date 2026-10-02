using System;
using System.Text;

namespace Vip.RestClient.Tests.Utils;

internal static class JwtHelper
{
    #region Metodos Publicos

    public static string Token(string header, string payload, byte[] signature)
    {
        return Base64Url(Encoding.UTF8.GetBytes(header)) + "." + Base64Url(Encoding.UTF8.GetBytes(payload)) + "." + Base64Url(signature);
    }

    #endregion

    #region Métodos Privados

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    #endregion
}