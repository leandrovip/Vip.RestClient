using System;
using System.IO;
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

        Assert.Equal(expected, ApiSurface.Render(typeof(ClientApi).Assembly));
    }
}
