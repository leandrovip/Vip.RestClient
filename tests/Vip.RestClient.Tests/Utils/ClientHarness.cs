using System;
using Newtonsoft.Json;

namespace Vip.RestClient.Tests.Utils;

internal sealed class ClientHarness : IDisposable
{
    #region Propriedades

    public FakeHandler Handler { get; }
    public ClientApi Client { get; }

    #endregion

    #region Construtores

    public ClientHarness(FakeHandler handler, string baseUrl = "https://api.example.com/root/", JsonSerializerSettings settings = null)
    {
        Handler = handler;
        Client = new ClientApi(baseUrl, handler, settings);
    }

    #endregion

    #region Métodos Públicos

    public void Dispose()
    {
        Client.ConfigureHttpClient(client => client.Dispose());
    }

    #endregion
}