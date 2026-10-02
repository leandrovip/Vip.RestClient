using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Vip.RestClient;
using Vip.RestClient.Tests.Utils;
using Vip.RestClient.Tests.Utils.Contents;
using Vip.RestClient.Tests.Utils.Dtos;
using Vip.RestClient.Tests.Utils.Models;
using Xunit;

namespace Vip.RestClient.Tests;

public class ExternalHttpClientTests
{
    [Fact]
    public void Existing_and_derived_constructor_call_sites_remain_unambiguous()
    {
        Func<ClientApi> positionalNulls = () => new ClientApi(null, null, null);
        Func<ClientApi> namedNulls = () => new ClientApi(baseUrl: null, clientHandler: null, jsonSerializerSettings: null);
        Func<ClientApi> originalConstructor = () => new ClientApi("https://api.example.com/", clientHandler: new HttpClientHandler(), jsonSerializerSettings: null);
        Func<ClientApi> externalFactory = () => ClientApi.FromHttpClient(null, null, null);
        Func<LegacyConstructorConsumer> derivedConstructor = () => new LegacyConstructorConsumer();

        Assert.NotNull(positionalNulls);
        Assert.NotNull(namedNulls);
        Assert.NotNull(originalConstructor);
        Assert.NotNull(externalFactory);
        Assert.NotNull(derivedConstructor);
    }

    [Fact]
    public void Factory_validates_client_first_then_base_url_and_uri()
    {
        var missingClient = Assert.Throws<ArgumentNullException>(() => ClientApi.FromHttpClient("https://[", null));
        Assert.Equal("httpClient", missingClient.ParamName);

        using var handler = new FakeHandler(_ => ResponseHelper.Text("ok"));
        using var httpClient = new HttpClient(handler);
        var missingBaseUrl = Assert.Throws<ArgumentNullException>(() => ClientApi.FromHttpClient(null, httpClient));
        Assert.Equal("baseUrl", missingBaseUrl.ParamName);
        Assert.Throws<UriFormatException>(() => ClientApi.FromHttpClient("https://[", httpClient));
    }

    [Theory]
    [InlineData("https://api.example.com/base", "items", "https://api.example.com/base/", "https://api.example.com/base/items")]
    [InlineData("https://api.example.com/base/", "items", "https://api.example.com/base/", "https://api.example.com/base/items")]
    [InlineData("https://api.example.com/base/", "/rooted", "https://api.example.com/base/", "https://api.example.com/rooted")]
    [InlineData("https://api.example.com/base/", "https://api.example.com/absolute", "https://api.example.com/base/", "https://api.example.com/absolute")]
    public async Task Factory_base_url_resolves_requests_independently_of_httpclient_base_address(string baseUrl, string endpoint, string expectedBaseUri, string expectedRequestUri)
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("ok"));
        using var httpClient = new HttpClient(handler) {BaseAddress = new Uri("https://api.example.com/external/")};
        var originalBaseAddress = httpClient.BaseAddress;
        var api = ClientApi.FromHttpClient(baseUrl, httpClient);

        await api.GetAsync<string>(endpoint);

        Assert.Equal(new Uri(expectedBaseUri), api.BaseUri);
        Assert.Equal(new Uri(expectedRequestUri), handler.RequestUri);
        Assert.Equal(originalBaseAddress, httpClient.BaseAddress);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Factory_preserves_client_headers_timeout_address_and_handler_decompression(bool hasAccept)
    {
        using var handler = new FakeHandler(_ => ResponseHelper.Text("unused"));
        handler.AutomaticDecompression = DecompressionMethods.Deflate;
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com/external/"),
            Timeout = TimeSpan.FromSeconds(17)
        };
        if (hasAccept) httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/plain");
        var originalBaseAddress = httpClient.BaseAddress;
        var originalTimeout = httpClient.Timeout;
        var api = ClientApi.FromHttpClient("https://api.example.com/base", httpClient);
        HttpClient configuredClient = null;

        api.ConfigureHttpClient(client => configuredClient = client);

        Assert.Same(httpClient, configuredClient);
        Assert.Equal(originalBaseAddress, httpClient.BaseAddress);
        Assert.Equal(originalTimeout, httpClient.Timeout);
        Assert.Equal(hasAccept ? "text/plain" : "", httpClient.DefaultRequestHeaders.Accept.ToString());
        Assert.Equal(DecompressionMethods.Deflate, handler.AutomaticDecompression);
    }

    [Fact]
    public void Client_mutators_and_configuration_change_the_shared_httpclient()
    {
        using var handler = new FakeHandler(_ => ResponseHelper.Text("unused"));
        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Mode", "initial");
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient);

        api.SetHeader("X-Mode", "updated");
        api.SetAuthorizationBearer("synthetic-token");
        Assert.Equal("updated", Assert.Single(httpClient.DefaultRequestHeaders.GetValues("X-Mode")));
        Assert.Equal("Bearer synthetic-token", httpClient.DefaultRequestHeaders.Authorization.ToString());

        api.RemoveAuthorization();
        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
        api.SetAuthorization("Basic synthetic-credential");
        Assert.Equal("Basic synthetic-credential", httpClient.DefaultRequestHeaders.Authorization.ToString());

        api.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(19));
        Assert.Equal(TimeSpan.FromSeconds(19), httpClient.Timeout);
    }

    [Fact]
    public async Task Multiple_wrappers_share_external_client_and_owner_can_send_directly()
    {
        var requests = new List<Uri>();
        var handler = new FakeHandler(request =>
        {
            requests.Add(request.RequestUri);
            return ResponseHelper.Text("ok");
        });
        using var httpClient = new HttpClient(handler);
        var first = ClientApi.FromHttpClient("https://api.example.com/first/", httpClient);
        var second = ClientApi.FromHttpClient("https://api.example.com/second/", httpClient);

        await first.GetAsync<string>("items");
        await second.GetAsync<string>("items");
        using var directResponse = await httpClient.GetAsync("https://api.example.com/direct");

        Assert.Equal(new[]
        {
            new Uri("https://api.example.com/first/items"),
            new Uri("https://api.example.com/second/items"),
            new Uri("https://api.example.com/direct")
        }, requests);
        Assert.Equal(3, handler.SendCount);
    }

    [Fact]
    public async Task Json_settings_apply_to_output_only_and_response_read_uses_default_settings()
    {
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new PrefixStringConverter());
        var handler = new FakeHandler(_ => ResponseHelper.Text("{\"Value\":\"server\"}"));
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient, settings);

        var response = await api.PostAsync<Payload>("items", new Payload {Value = "client"});

        Assert.Equal("{\"Value\":\"written:client\"}", handler.RequestBody);
        Assert.Equal("server", response.Data.Value);
    }

    [Fact]
    public async Task Request_and_response_content_are_disposed_without_disposing_external_client()
    {
        var requestContent = new TrackingContent();
        var responseContent = new TrackingContent();
        var sendCount = 0;
        var handler = new FakeHandler(_ => ++sendCount == 1
            ? new HttpResponseMessage(HttpStatusCode.OK) {Content = responseContent}
            : ResponseHelper.Text("direct"));
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient);

        await api.PostAsync<string>("items", requestContent);

        Assert.True(requestContent.WasDisposed);
        Assert.True(responseContent.WasDisposed);
        using var directResponse = await httpClient.GetAsync("https://api.example.com/direct");
        Assert.Equal(2, handler.SendCount);
    }

    [Fact]
    public async Task Parsing_failure_leaves_owner_client_usable_and_event_uri_original_after_before_send_change()
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("not-json"));
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/base", httpClient);
        ResponseEvent received = null;
        api.BeforeSend += (_, request) => request.RequestUri = new Uri("https://api.example.com/changed");
        api.ResponseDataReceived += (_, args) => received = args;

        await Assert.ThrowsAsync<JsonReaderException>(() => api.GetAsync<Payload>("items"));

        Assert.Equal(new Uri("https://api.example.com/changed"), handler.RequestUri);
        Assert.Equal(new Uri("https://api.example.com/base/items"), received.Uri);
        using var directResponse = await httpClient.GetAsync("https://api.example.com/direct");
        Assert.Equal(2, handler.SendCount);
    }
}
