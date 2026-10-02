using System.Net;
using System.Net.Http;

namespace Vip.RestClient.Tests.Utils;

internal static class ResponseHelper
{
    #region Métodos

    public static HttpResponseMessage Text(string content, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status) {Content = new StringContent(content)};
    }

    #endregion
}