using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Vip.RestClient.Tests.Utils;
using Vip.RestClient.Tests.Utils.Contents;
using Vip.RestClient.Tests.Utils.Dtos;
using Vip.RestClient.Tests.Utils.Models;
using Xunit;

namespace Vip.RestClient.Tests;

public class ClientApiTests
{
    [Theory]
    [InlineData("GET", "get", null)]
    [InlineData("POST", "post-empty", null)]
    [InlineData("PUT", "put-object", "{\"Name\":\"client\"}")]
    [InlineData("PATCH", "patch-object", "{\"Name\":\"client\"}")]
    [InlineData("DELETE", "delete", null)]
    [InlineData("OPTIONS", "options", null)]
    public async Task Untyped_calls_keep_their_current_verbs_and_body_shapes(string expectedMethod, string call, string expectedBody)
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("ignored"));
        using var harness = new ClientHarness(handler);

        switch (call)
        {
            case "get":          await harness.Client.GetAsync("items"); break;
            case "post-empty":   await harness.Client.PostAsync("items"); break;
            case "put-object":   await harness.Client.PutAsync("items", new {Name = "client"}); break;
            case "patch-object": await harness.Client.PatchAsync("items", new {Name = "client"}); break;
            case "delete":       await harness.Client.DeleteAsync("items"); break;
            default:             await harness.Client.OptionsAsync("items", new[] {new KeyValuePair<string, string>("X-Mode", "probe")}); break;
        }

        Assert.Equal(expectedMethod, handler.Method);
        Assert.Equal(expectedBody, handler.RequestBody);
        Assert.Contains("application/json", handler.Accept);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Typed_calls_send_their_current_verbs(string method)
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("ok"));
        using var harness = new ClientHarness(handler);

        switch (method)
        {
            case "GET":   await harness.Client.GetAsync<string>("items"); break;
            case "POST":  await harness.Client.PostAsync<string>("items", (object) new {Name = "client"}); break;
            case "PUT":   await harness.Client.PutAsync<string>("items", new {Name = "client"}); break;
            case "PATCH": await harness.Client.PatchAsync<string>("items", new {Name = "client"}); break;
            default:      await harness.Client.DeleteAsync<string>("items"); break;
        }

        Assert.Equal(method, handler.Method);
    }

    [Fact]
    public async Task Typed_post_sends_content_directly_and_accepts_null_content()
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("ok"));
        using var harness = new ClientHarness(handler);
        await harness.Client.PostAsync<string>("direct", new StringContent("plain", Encoding.UTF8, "text/plain"));
        Assert.Equal("plain", handler.RequestBody);
        await harness.Client.PostAsync<string>("empty", (HttpContent) null);
        Assert.Null(handler.RequestBody);
    }

    [Fact]
    public async Task Object_null_is_json_null_but_bodyless_post_has_no_content()
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("ok"));
        using var harness = new ClientHarness(handler);
        await harness.Client.PostAsync<string>("null", (object) null);
        Assert.Equal("null", handler.RequestBody);
        await harness.Client.PostAsync("empty");
        Assert.Null(handler.RequestBody);
    }

    [Theory]
    [InlineData("items", "https://api.example.com/root/items")]
    [InlineData("/items", "https://api.example.com/items")]
    [InlineData("https://api.example.com/other", "https://api.example.com/other")]
    public async Task Endpoint_uri_uses_dotnet_relative_absolute_and_root_resolution(string endpoint, string expected)
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("ok"));
        using var harness = new ClientHarness(handler);
        await harness.Client.GetAsync(endpoint);
        Assert.Equal(new Uri(expected), handler.RequestUri);
    }

    [Fact]
    public async Task Serializer_settings_apply_to_output_but_not_typed_response_reading()
    {
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new PrefixStringConverter());
        var handler = new FakeHandler(_ => ResponseHelper.Text("{\"Value\":\"server\"}"));
        using var harness = new ClientHarness(handler, settings: settings);

        var result = await harness.Client.PostAsync<Payload>("items", new Payload {Value = "client"});

        Assert.Equal("{\"Value\":\"written:client\"}", handler.RequestBody);
        Assert.Equal("server", result.Data.Value);
    }

    [Fact]
    public async Task Invalid_uri_faults_task_before_serializing_throwing_payload()
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("unused"));
        using var harness = new ClientHarness(handler);
        ThrowingPayload.GetterCalled = false;

        Task<Response> call = harness.Client.PostAsync("http://[", new ThrowingPayload());

        Assert.NotNull(call);
        await Assert.ThrowsAsync<UriFormatException>(() => call);
        Assert.False(ThrowingPayload.GetterCalled);
        Assert.Equal(0, handler.SendCount);
    }

    [Fact]
    public void Configure_callback_exceptions_are_synchronous()
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("unused"));
        using var harness = new ClientHarness(handler);
        Assert.Throws<InvalidOperationException>(() => harness.Client.ConfigureHttpClient(_ => throw new InvalidOperationException("configuration")));
    }

    [Fact]
    public async Task Before_send_can_change_request_uri_while_response_event_keeps_resolved_uri()
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("ok"));
        using var harness = new ClientHarness(handler);
        ResponseEvent received = null;
        harness.Client.BeforeSend += (_, request) => request.RequestUri = new Uri("https://api.example.com/changed");
        harness.Client.ResponseDataReceived += (_, args) => received = args;

        await harness.Client.GetAsync<string>("items");

        Assert.Equal(new Uri("https://api.example.com/changed"), handler.RequestUri);
        Assert.Equal(new Uri("https://api.example.com/root/items"), received.Uri);
    }

    [Fact]
    public async Task Before_send_handler_runs_before_transport_handler()
    {
        var order = new List<string>();
        var handler = new FakeHandler(_ =>
        {
            order.Add("send");
            return ResponseHelper.Text("ok");
        });
        using var harness = new ClientHarness(handler);
        harness.Client.BeforeSend += (_, _) => order.Add("before");

        await harness.Client.GetAsync("items");

        Assert.Equal(new[] {"before", "send"}, order);
    }

    [Fact]
    public async Task Before_send_runs_before_transport_and_throwing_handler_prevents_send()
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("unused"));
        using var harness = new ClientHarness(handler);
        var order = new List<string>();
        harness.Client.BeforeSend += (_, _) =>
        {
            order.Add("before");
            throw new InvalidOperationException("before");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Client.GetAsync("items"));

        Assert.Equal(new[] {"before"}, order);
        Assert.Equal(0, handler.SendCount);
    }

    [Fact]
    public async Task Response_event_occurs_after_read_and_before_json_parse()
    {
        const string malformed = "not-json";
        var handler = new FakeHandler(_ => ResponseHelper.Text(malformed));
        using var harness = new ClientHarness(handler);
        ResponseEvent observed = null;
        harness.Client.ResponseDataReceived += (_, args) => observed = args;

        await Assert.ThrowsAsync<JsonReaderException>(() => harness.Client.GetAsync<Payload>("items"));

        Assert.NotNull(observed);
        Assert.Equal(malformed, observed.Content);
        Assert.True(observed.Success);
        Assert.Equal((int) HttpStatusCode.OK, observed.StatusCode);
        Assert.NotEqual(default, observed.Received);
    }

    [Fact]
    public async Task Throwing_response_event_prevents_json_parse()
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("not-json"));
        using var harness = new ClientHarness(handler);
        harness.Client.ResponseDataReceived += (_, _) => throw new InvalidOperationException("event");

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Client.GetAsync<Payload>("items"));
        Assert.Equal(1, handler.SendCount);
    }

    [Theory]
    [InlineData("%7B%22Value%22%3A%22decoded%22%7D", "{\"Value\":\"decoded\"}")]
    [InlineData("%7b%22Value%22%3A%22unchanged\"%7D", "%7b%22Value%22%3A%22unchanged\"%7D")]
    public async Task String_response_decodes_only_uppercase_literal_percent_7b(string body, string expected)
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text(body));
        using var harness = new ClientHarness(handler);
        ResponseEvent observed = null;
        harness.Client.ResponseDataReceived += (_, args) => observed = args;

        var response = await harness.Client.GetAsync<string>("items");

        Assert.Equal(expected, response.Data);
        Assert.Equal(body, observed.Content);
    }

    [Fact]
    public async Task Binary_response_event_has_null_content_and_stream_is_closed_with_response()
    {
        var stream = new TrackingStream(new byte[] {1, 2, 3});
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {Content = new StreamContent(stream)});
        using var harness = new ClientHarness(handler);
        ResponseEvent received = null;
        harness.Client.ResponseDataReceived += (_, args) => received = args;

        var response = await harness.Client.GetAsync<Stream>("items");

        Assert.Null(received.Content);
        Assert.NotNull(response.Data);
        Assert.False(response.Data.CanRead);
        Assert.True(stream.WasDisposed);
    }

    [Fact]
    public async Task Stream_error_still_populates_data_and_leaves_error_text_null()
    {
        var stream = new TrackingStream(new byte[] {4, 5});
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError) {Content = new StreamContent(stream)});
        using var harness = new ClientHarness(handler);
        ResponseEvent received = null;
        harness.Client.ResponseDataReceived += (_, args) => received = args;

        var response = await harness.Client.GetAsync<Stream>("items");

        Assert.False(response.IsSuccessStatusCode);
        Assert.NotNull(response.Data);
        Assert.Null(response.ErrorResponseData);
        Assert.Null(received.Content);
        Assert.True(stream.WasDisposed);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    public async Task Byte_array_data_is_read_for_success_and_error_without_copying_error_text(int statusCode)
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage((HttpStatusCode) statusCode) {Content = new ByteArrayContent(new byte[] {9, 8})});
        using var harness = new ClientHarness(handler);
        ResponseEvent received = null;
        harness.Client.ResponseDataReceived += (_, args) => received = args;
        var response = await harness.Client.GetAsync<byte[]>("items");
        Assert.Equal(new byte[] {9, 8}, response.Data);
        Assert.Null(response.ErrorResponseData);
        Assert.Null(received.Content);
        Assert.Equal(statusCode == 200, response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Generic_http_error_keeps_string_data_default_and_error_text()
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("failure", HttpStatusCode.BadRequest));
        using var harness = new ClientHarness(handler);
        var response = await harness.Client.GetAsync<Payload>("items");
        Assert.False(response.IsSuccessStatusCode);
        Assert.Null(response.Data);
        Assert.Equal("failure", response.ErrorResponseData);
    }

    [Fact]
    public async Task Generic_string_http_error_keeps_error_body_separate_from_data()
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("error text", HttpStatusCode.BadRequest));
        using var harness = new ClientHarness(handler);
        var response = await harness.Client.GetAsync<string>("items");
        Assert.Null(response.Data);
        Assert.Equal("error text", response.ErrorResponseData);
    }

    [Fact]
    public async Task Non_generic_success_does_not_read_response_body_and_error_does()
    {
        var untouched = new CountingContent("success body");
        var successHandler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {Content = untouched});
        using (var harness = new ClientHarness(successHandler))
        {
            var responseEventRaised = false;
            harness.Client.ResponseDataReceived += (_, _) => responseEventRaised = true;
            var response = await harness.Client.GetAsync("items");
            Assert.True(response.IsSuccessStatusCode);
            Assert.False(untouched.WasRead);
            Assert.False(responseEventRaised);
        }

        var errorHandler = new FakeHandler(_ => ResponseHelper.Text("error body", HttpStatusCode.BadRequest));
        using var errorHarness = new ClientHarness(errorHandler);
        var error = await errorHarness.Client.GetAsync("items");
        Assert.Equal("error body", error.ErrorResponseData);
    }

    [Fact]
    public async Task Non_generic_error_read_exposes_aggregate_exception_while_generic_await_unwraps()
    {
        var untypedHandler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) {Content = new FaultingContent()});
        using (var harness = new ClientHarness(untypedHandler))
            await Assert.ThrowsAsync<AggregateException>(() => harness.Client.GetAsync("items"));

        var typedHandler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) {Content = new FaultingContent()});
        using var typedHarness = new ClientHarness(typedHandler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => typedHarness.Client.GetAsync<string>("items"));
    }

    [Fact]
    public async Task Response_envelope_retains_status_headers_reason_request_and_error_data()
    {
        var handler = new FakeHandler(_ =>
        {
            var response = ResponseHelper.Text("bad", HttpStatusCode.BadRequest);
            response.ReasonPhrase = "Synthetic failure";
            response.Headers.Add("X-Trace", "synthetic");
            response.Content.Headers.Add("X-Content", "marker");
            return response;
        });
        using var harness = new ClientHarness(handler);

        var response = await harness.Client.GetAsync("items");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Synthetic failure", response.ReasonPhrase);
        Assert.Equal("synthetic", Assert.Single(response.Headers.GetValues("X-Trace")));
        Assert.Equal("marker", Assert.Single(response.ContentHeaders.GetValues("X-Content")));
        Assert.Same(handler.Request, response.RequestMessage);
        Assert.Equal("bad", response.ErrorResponseData);
    }

    [Fact]
    public async Task Request_content_is_disposed_after_normal_call_but_options_request_is_not()
    {
        var content = new TrackingContent();
        var handler = new FakeHandler(_ => ResponseHelper.Text("ok"));
        using var harness = new ClientHarness(handler);
        await harness.Client.PostAsync<string>("items", content);
        Assert.True(content.WasDisposed);

        var optionsHandler = new FakeHandler(_ => ResponseHelper.Text("ok"));
        using var optionsHarness = new ClientHarness(optionsHandler);
        await optionsHarness.Client.OptionsAsync("items", Array.Empty<KeyValuePair<string, string>>());
        using (var optionsRequest = optionsHandler.Request)
        {
            optionsRequest.Content = new StringContent("still mutable");
            Assert.Equal("still mutable", await optionsRequest.Content.ReadAsStringAsync());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Http_errors_are_envelopes_until_ensure_success_is_called(bool typed)
    {
        var handler = new FakeHandler(_ => ResponseHelper.Text("{\"Code\":\"bad\"}", HttpStatusCode.BadRequest));
        using var harness = new ClientHarness(handler);
        if (typed)
        {
            var response = await harness.Client.GetAsync<string>("items");
            var exception = Assert.Throws<UnsuccessfulStatusCodeException<ErrorDto>>(response.EnsureSuccessStatusCode<ErrorDto>);
            Assert.Equal("bad", exception.ErrorInformation.Code);
            Assert.Same(response, exception.Response);
        }
        else
        {
            var response = await harness.Client.GetAsync("items");
            var exception = Assert.Throws<UnsuccessfulStatusCodeException>(response.EnsureSuccessStatusCode);
            Assert.Same(response, exception.Response);
        }
    }
}