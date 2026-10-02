using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Vip.RestClient.Tests.Utils;
using Vip.RestClient.Tests.Utils.Contents;
using Vip.RestClient.Tests.Utils.Dtos;
using Xunit;

namespace Vip.RestClient.Tests;

public class SendAsyncCancellationTests
{
    [Fact]
    public async Task Untyped_send_faults_its_task_for_null_request_and_missing_uri()
    {
        var handler = new AsyncFakeHandler((_, _) => Task.FromResult(ResponseHelper.Text("unused")));
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/base", httpClient);

        Task<Response> nullRequest = api.SendAsync((HttpRequestMessage) null, CancellationToken.None);
        Assert.NotNull(nullRequest);
        var nullError = await Assert.ThrowsAsync<ArgumentNullException>(() => nullRequest);
        Assert.Equal("request", nullError.ParamName);

        using var missingUri = new HttpRequestMessage();
        Task<Response> missingUriTask = api.SendAsync(missingUri, CancellationToken.None);
        var uriError = await Assert.ThrowsAsync<ArgumentException>(() => missingUriTask);
        Assert.Equal("request", uriError.ParamName);
        Assert.Contains("request URI", uriError.Message);
        Assert.Equal(0, handler.SendCount);
    }

    [Fact]
    public async Task Typed_send_faults_its_task_for_null_request_and_missing_uri()
    {
        var handler = new AsyncFakeHandler((_, _) => Task.FromResult(ResponseHelper.Text("unused")));
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/base", httpClient);

        Task<Response<string>> nullRequest = api.SendAsync<string>((HttpRequestMessage) null, CancellationToken.None);
        Assert.NotNull(nullRequest);
        var nullError = await Assert.ThrowsAsync<ArgumentNullException>(() => nullRequest);
        Assert.Equal("request", nullError.ParamName);

        using var missingUri = new HttpRequestMessage();
        Task<Response<string>> missingUriTask = api.SendAsync<string>(missingUri, CancellationToken.None);
        var uriError = await Assert.ThrowsAsync<ArgumentException>(() => missingUriTask);
        Assert.Equal("request", uriError.ParamName);
        Assert.Contains("request URI", uriError.Message);
        Assert.Equal(0, handler.SendCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pre_cancelled_token_skips_before_send_and_transport(bool typed)
    {
        var handler = new AsyncFakeHandler((_, _) => Task.FromResult(ResponseHelper.Text("unused")));
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/base", httpClient);
        var beforeSendCount = 0;
        api.BeforeSend += (_, _) => beforeSendCount++;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("items", UriKind.Relative));
        Task operation;

        if (typed)
            operation = api.SendAsync<string>(request, cancellation.Token);
        else
            operation = api.SendAsync(request, cancellation.Token);

        Assert.True(operation.IsCanceled);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Equal(new Uri("https://api.example.com/base/items"), request.RequestUri);
        Assert.Equal(0, beforeSendCount);
        Assert.Equal(0, handler.SendCount);
    }

    [Theory]
    [InlineData("items", "https://api.example.com/base/items")]
    [InlineData("https://api.example.com/absolute", "https://api.example.com/absolute")]
    public async Task Typed_send_resolves_uri_before_event_and_event_keeps_original_uri(string endpoint, string resolvedUri)
    {
        var handler = new AsyncFakeHandler((_, _) => Task.FromResult(ResponseHelper.Text("{\"Value\":\"ok\"}")));
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/base", httpClient);
        ResponseEvent received = null;
        api.BeforeSend += (_, request) => request.RequestUri = new Uri("https://api.example.com/changed");
        api.ResponseDataReceived += (_, args) => received = args;
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, UriKind.RelativeOrAbsolute));

        var response = await api.SendAsync<Payload>(requestMessage, CancellationToken.None);

        Assert.Equal("ok", response.Data.Value);
        Assert.Equal(new Uri("https://api.example.com/changed"), handler.Request.RequestUri);
        Assert.Equal(new Uri(resolvedUri), received.Uri);
    }

    [Fact]
    public async Task ResponseContentRead_buffers_untyped_typed_string_and_binary_responses()
    {
        var untypedContent = new CountingContent("untyped body");
        var jsonContent = new CountingContent("{\"Value\":\"typed\"}");
        var stringContent = new CountingContent("plain response");
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK) {Content = untypedContent},
            new HttpResponseMessage(HttpStatusCode.OK) {Content = jsonContent},
            new HttpResponseMessage(HttpStatusCode.OK) {Content = stringContent},
            new HttpResponseMessage(HttpStatusCode.BadRequest) {Content = new ByteArrayContent(new byte[] {4, 5})}
        });
        var handler = new FakeHandler(_ => responses.Dequeue());
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient);

        using var untypedRequest = new HttpRequestMessage(HttpMethod.Get, "untyped");
        var untypedResponse = await api.SendAsync(untypedRequest, CancellationToken.None);
        using var jsonRequest = new HttpRequestMessage(HttpMethod.Get, "json");
        var jsonResponse = await api.SendAsync<Payload>(jsonRequest, CancellationToken.None);
        using var stringRequest = new HttpRequestMessage(HttpMethod.Get, "string");
        var stringResponse = await api.SendAsync<string>(stringRequest, CancellationToken.None);
        using var bytesRequest = new HttpRequestMessage(HttpMethod.Get, "bytes");
        var bytesResponse = await api.SendAsync<byte[]>(bytesRequest, CancellationToken.None);

        Assert.True(untypedResponse.IsSuccessStatusCode);
        Assert.True(untypedContent.WasRead);
        Assert.Equal("typed", jsonResponse.Data.Value);
        Assert.True(jsonContent.WasRead);
        Assert.Equal("plain response", stringResponse.Data);
        Assert.True(stringContent.WasRead);
        Assert.Equal(new byte[] {4, 5}, bytesResponse.Data);
        Assert.Null(bytesResponse.ErrorResponseData);
        Assert.Equal(HttpStatusCode.BadRequest, bytesResponse.StatusCode);
    }

    [Fact]
    public async Task Prepared_request_content_is_sent_directly_and_remains_caller_owned()
    {
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new PrefixStringConverter());
        var handler = new FakeHandler(_ => ResponseHelper.Text("{\"Value\":\"server\"}"));
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient, settings);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("items", UriKind.Relative));
        request.Content = new StringContent("{\"Value\":\"prepared\"}", Encoding.UTF8, "application/json");

        var response = await api.SendAsync<Payload>(request, CancellationToken.None);

        Assert.Equal("{\"Value\":\"prepared\"}", handler.RequestBody);
        Assert.Equal("server", response.Data.Value);
        Assert.Equal("{\"Value\":\"prepared\"}", await request.Content.ReadAsStringAsync());
        Assert.Equal(new Uri("https://api.example.com/items"), request.RequestUri);
    }

    [Fact]
    public async Task Transport_cancellation_leaves_request_owned_and_client_usable()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCount = 0;
        var handler = new AsyncFakeHandler(async (_, cancellationToken) =>
        {
            if (callbackCount++ == 0)
            {
                entered.TrySetResult(true);
                await release.Task.WaitAsync(cancellationToken);
            }

            return ResponseHelper.Text("ok");
        });
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient);
        using var cancellation = new CancellationTokenSource();
        var requestContent = new TrackingContent();
        using var request = new HttpRequestMessage(HttpMethod.Post, "blocked");
        request.Content = requestContent;
        var sendTask = api.SendAsync(request, cancellation.Token);

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sendTask.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(requestContent.WasDisposed);
            Assert.Equal(1, handler.SendCount);
        }
        finally
        {
            release.TrySetResult(true);
        }

        using var retryRequest = new HttpRequestMessage(HttpMethod.Get, "retry");
        var retryResponse = await api.SendAsync(retryRequest, CancellationToken.None);
        Assert.True(retryResponse.IsSuccessStatusCode);
        Assert.Equal(2, handler.SendCount);
    }

    [Fact]
    public async Task Cancellation_during_response_body_buffering_is_cooperative()
    {
        var blockingContent = new BlockingContent();
        var requestContent = new TrackingContent();
        var callbackCount = 0;
        var handler = new AsyncFakeHandler((_, _) => Task.FromResult(
            ++callbackCount == 1
                ? new HttpResponseMessage(HttpStatusCode.OK) {Content = blockingContent}
                : ResponseHelper.Text("{\"Value\":\"retry\"}")));
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient);
        var responseEventRaised = false;
        api.ResponseDataReceived += (_, _) => responseEventRaised = true;
        using var cancellation = new CancellationTokenSource();
        using var request = new HttpRequestMessage(HttpMethod.Get, "buffer");
        request.Content = requestContent;
        var sendTask = api.SendAsync<Payload>(request, cancellation.Token);

        try
        {
            await blockingContent.Started.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sendTask.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(requestContent.WasDisposed);
            Assert.False(responseEventRaised);
            Assert.Equal(1, handler.SendCount);
        }
        finally
        {
            blockingContent.Release();
            if (!sendTask.IsCompleted)
                try { await sendTask.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch { }
        }

        using var directResponse = await httpClient.GetAsync("https://api.example.com/direct");
        Assert.Equal(2, handler.SendCount);
    }

    [Fact]
    public async Task Response_is_disposed_on_parse_failure_but_request_remains_caller_owned()
    {
        var requestContent = new TrackingContent();
        var responseContent = new BlockingContent();
        var callbackCount = 0;
        var handler = new AsyncFakeHandler((_, _) => Task.FromResult(++callbackCount == 1
            ? new HttpResponseMessage(HttpStatusCode.OK) {Content = responseContent}
            : ResponseHelper.Text("ok")));
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient);
        using var request = new HttpRequestMessage(HttpMethod.Post, "parse");
        request.Content = requestContent;
        var sendTask = api.SendAsync<Payload>(request, CancellationToken.None);

        await responseContent.Started.WaitAsync(TimeSpan.FromSeconds(10));
        responseContent.Release();
        await Assert.ThrowsAsync<JsonReaderException>(() => sendTask);

        Assert.True(responseContent.WasDisposed);
        Assert.False(requestContent.WasDisposed);
        using var directResponse = await httpClient.GetAsync("https://api.example.com/direct");
        Assert.Equal(2, handler.SendCount);
    }

    [Fact]
    public async Task SendAsync_obeys_external_maximum_response_buffer_size()
    {
        var callbackCount = 0;
        var handler = new FakeHandler(_ => ++callbackCount == 1
            ? new HttpResponseMessage(HttpStatusCode.OK) {Content = new CountingContent("too large")}
            : ResponseHelper.Text("ok"));
        using var httpClient = new HttpClient(handler);
        httpClient.MaxResponseContentBufferSize = 4;
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient);
        using var oversizedRequest = new HttpRequestMessage(HttpMethod.Get, "large");

        await Assert.ThrowsAsync<HttpRequestException>(() => api.SendAsync(oversizedRequest, CancellationToken.None));

        using var request = new HttpRequestMessage(HttpMethod.Get, "small");
        var response = await api.SendAsync(request, CancellationToken.None);
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(2, handler.SendCount);
    }
}