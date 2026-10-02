using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Vip.RestClient.Tests.Utils;

internal sealed class AsyncFakeHandler : HttpClientHandler
{
    #region Propriedades

    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

    public int SendCount { get; private set; }
    public HttpRequestMessage Request { get; private set; }

    #endregion

    #region Construtores

    public AsyncFakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    #endregion

    #region Métodos

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SendCount++;
        Request = request;
        var response = await _respond(request, cancellationToken);
        if (response.RequestMessage == null) response.RequestMessage = request;
        return response;
    }

    #endregion
}