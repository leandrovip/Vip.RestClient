using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Vip.RestClient.Tests.Utils;

internal sealed class FakeHandler : HttpClientHandler
{
    #region Propriedades

    public int SendCount { get; private set; }
    public HttpRequestMessage Request { get; private set; }
    public string Method { get; private set; }
    public Uri RequestUri { get; private set; }
    public string RequestBody { get; private set; }
    public string Accept { get; private set; }

    #endregion

    #region Fields

    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    #endregion

    #region Construtores

    public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    #endregion

    #region M�todos P�blicos

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SendCount++;
        Request = request;
        Method = request.Method.Method;
        RequestUri = request.RequestUri;
        Accept = string.Join(",", request.Headers.Accept);
        RequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync();

        var response = _respond(request);
        if (response.RequestMessage == null) response.RequestMessage = request;
        return response;
    }

    #endregion
}
