using System;
using System.IO;
using System.Linq;
using Vip.RestClient.Tests.Utils;
using Xunit;

namespace Vip.RestClient.Tests;

public class ApiSurfaceCharacterizationTests
{
    [Fact]
    public void Public_and_protected_declared_api_matches_frozen_baseline()
    {
        var expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "ApiSurface.baseline.txt"));
        var actual = ApiSurface.Render(typeof(ClientApi).Assembly);
        const string factory = "M public static Vip.RestClient.ClientApi FromHttpClient(System.String baseUrl,System.Net.Http.HttpClient httpClient,Newtonsoft.Json.JsonSerializerSettings jsonSerializerSettings=null)";
        const string send = "M public instance System.Threading.Tasks.Task<Vip.RestClient.Response> SendAsync(System.Net.Http.HttpRequestMessage request,System.Threading.CancellationToken cancellationToken)";
        const string sendTyped = "M public instance System.Threading.Tasks.Task<Vip.RestClient.Response<T>> SendAsync<T>(System.Net.Http.HttpRequestMessage request,System.Threading.CancellationToken cancellationToken)";

        Assert.Single(actual, line => line == factory);
        Assert.Single(actual, line => line == send);
        Assert.Single(actual, line => line == sendTyped);
        Assert.Equal(expected, actual.Where(line => line != factory && line != send && line != sendTyped));
    }
}