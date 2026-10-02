using System;
using System.IO;
using System.Linq;
using Vip.RestClient;
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
        const string approvedAddition = "M public static Vip.RestClient.ClientApi FromHttpClient(System.String baseUrl,System.Net.Http.HttpClient httpClient,Newtonsoft.Json.JsonSerializerSettings jsonSerializerSettings=null)";

        Assert.Single(actual, line => line == approvedAddition);
        Assert.Equal(expected, actual.Where(line => line != approvedAddition));
    }
}
