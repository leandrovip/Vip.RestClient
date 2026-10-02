using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Vip.RestClient.Tests.Utils;
using Vip.RestClient.Tests.Utils.Contents;
using Vip.RestClient.Tests.Utils.Streams;
using Xunit;

namespace Vip.RestClient.Tests;

public class DownloadStreamingTests
{
    [Fact]
    public async Task Download_faults_for_missing_endpoint_destination_or_unwritable_destination()
    {
        var handler = new AsyncFakeHandler((_, _) => Task.FromResult(ResponseHelper.Text("unused")));
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient);

        using var writable = new ObservedDestinationStream();
        Task<Response> endpointTask = api.DownloadAsync(null, writable, CancellationToken.None);
        Assert.NotNull(endpointTask);
        Assert.Equal("endpoint", (await Assert.ThrowsAsync<ArgumentNullException>(() => endpointTask)).ParamName);

        Task<Response> destinationTask = api.DownloadAsync("items", null, CancellationToken.None);
        Assert.Equal("destination", (await Assert.ThrowsAsync<ArgumentNullException>(() => destinationTask)).ParamName);

        using var readOnly = new MemoryStream(Array.Empty<byte>(), false);
        Task<Response> readOnlyTask = api.DownloadAsync("items", readOnly, CancellationToken.None);
        var readOnlyError = await Assert.ThrowsAsync<ArgumentException>(() => readOnlyTask);
        Assert.Equal("destination", readOnlyError.ParamName);
        Assert.Contains("writable", readOnlyError.Message);
        Assert.Equal(0, handler.SendCount);
    }

    [Fact]
    public async Task Pre_cancelled_token_skips_before_send()
    {
        var handler = new AsyncFakeHandler((_, _) => Task.FromResult(ResponseHelper.Text("unused")));
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/base", httpClient);
        var beforeSendCount = 0;
        api.BeforeSend += (_, _) => beforeSendCount++;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var destination = new ObservedDestinationStream();

        var task = api.DownloadAsync("items", destination, cancellation.Token);

        Assert.True(task.IsCanceled);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(0, beforeSendCount);
        Assert.Equal(0, handler.SendCount);
        Assert.Empty(destination.ToArray());
    }

    [Theory]
    [InlineData("items", "https://api.example.com/base/items")]
    [InlineData("https://api.example.com/absolute", "https://api.example.com/absolute")]
    public async Task Download_resolves_get_uri_before_before_send_and_transport(string endpoint, string expectedUri)
    {
        var order = "";
        var handler = new AsyncFakeHandler((_, _) =>
        {
            order += "send";
            return Task.FromResult(ResponseHelper.Text("unused"));
        });
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/base", httpClient);
        Uri uriBeforeSend = null;
        api.BeforeSend += (_, request) =>
        {
            order += "before";
            uriBeforeSend = request.RequestUri;
        };
        using var destination = new ObservedDestinationStream();

        var response = await api.DownloadAsync(endpoint, destination, CancellationToken.None);

        Assert.Equal("beforesend", order);
        Assert.Equal(new Uri(expectedUri), uriBeforeSend);
        Assert.Equal(HttpMethod.Get, handler.Request.Method);
        Assert.Equal(new Uri(expectedUri), handler.Request.RequestUri);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Download_streams_unknown_length_content_to_current_destination_position_without_buffer_limit_or_flush()
    {
        var bytes = new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12};
        var source = new ReadSourceStream(bytes, 3);
        long? contentLength = null;
        HttpResponseHeaders responseHeaders = null;
        HttpContentHeaders contentHeaders = null;
        var handler = new AsyncFakeHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(source)
            };
            contentLength = response.Content.Headers.ContentLength;
            responseHeaders = response.Headers;
            contentHeaders = response.Content.Headers;
            response.ReasonPhrase = "Synthetic download";
            response.Headers.Add("X-Download", "streamed");
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return Task.FromResult(response);
        });
        using var httpClient = new HttpClient(handler) {MaxResponseContentBufferSize = 4};
        var api = ClientApi.FromHttpClient("https://api.example.com/files/", httpClient);
        using var destination = new ObservedDestinationStream();
        destination.WriteByte(99);
        destination.Position = 1;

        var response = await api.DownloadAsync("item", destination, CancellationToken.None);

        Assert.Equal(new byte[] {99, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12}, destination.ToArray());
        Assert.Equal(destination.Length, destination.Position);
        Assert.Equal(0, destination.FlushCount);
        Assert.False(destination.WasDisposed);
        Assert.True(source.WasDisposed);
        Assert.Null(contentLength);
        Assert.Equal("application/octet-stream", response.ContentHeaders.ContentType.MediaType);
        Assert.Same(responseHeaders, response.Headers);
        Assert.Same(contentHeaders, response.ContentHeaders);
        Assert.Equal("streamed", Assert.Single(response.Headers.GetValues("X-Download")));
        Assert.Equal("Synthetic download", response.ReasonPhrase);
        Assert.Same(handler.Request, response.RequestMessage);
        Assert.Null(response.ErrorResponseData);
    }

    [Fact]
    public async Task Download_http_error_returns_metadata_without_reading_content_or_writing_destination()
    {
        var handler = new AsyncFakeHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new FaultingContent()
            };
            response.ReasonPhrase = "Synthetic failure";
            response.Headers.Add("X-Download", "rejected");
            return Task.FromResult(response);
        });
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient);
        using var destination = new ObservedDestinationStream();

        var response = await api.DownloadAsync("item", destination, CancellationToken.None);

        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("Synthetic failure", response.ReasonPhrase);
        Assert.Equal("rejected", Assert.Single(response.Headers.GetValues("X-Download")));
        Assert.Same(handler.Request, response.RequestMessage);
        Assert.Null(response.ErrorResponseData);
        Assert.Empty(destination.ToArray());
        Assert.Equal(0, destination.FlushCount);
        Assert.False(destination.WasDisposed);
        Assert.Throws<UnsuccessfulStatusCodeException>(response.EnsureSuccessStatusCode);
    }

    [Fact]
    public async Task Cancellation_during_source_copy_keeps_partial_destination_open_and_client_reusable()
    {
        var source = new ReadSourceStream(new byte[] {1, 2, 3, 4, 5, 6}, 2, 2);
        var retrySource = new ReadSourceStream(new byte[] {7, 8, 9}, 2);
        var callbackCount = 0;
        var handler = new AsyncFakeHandler((_, _) => Task.FromResult(++callbackCount == 1
            ? new HttpResponseMessage(HttpStatusCode.OK) {Content = new StreamContent(source)}
            : new HttpResponseMessage(HttpStatusCode.OK) {Content = new StreamContent(retrySource)}));
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient);
        using var cancellation = new CancellationTokenSource();
        using var destination = new ObservedDestinationStream();
        var downloadTask = api.DownloadAsync("blocked", destination, cancellation.Token);

        try
        {
            await source.BlockedRead.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(new byte[] {1, 2}, destination.ToArray());
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloadTask.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(destination.WasDisposed);
            Assert.True(source.WasDisposed);
        }
        finally
        {
            source.Release();
            if (!downloadTask.IsCompleted)
                try { await downloadTask.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch { }
        }

        using var retryDestination = new ObservedDestinationStream();
        var retryResponse = await api.DownloadAsync("retry", retryDestination, CancellationToken.None);
        Assert.True(retryResponse.IsSuccessStatusCode);
        Assert.Equal(new byte[] {7, 8, 9}, retryDestination.ToArray());
        Assert.Equal(2, handler.SendCount);
    }

    [Fact]
    public async Task Cancellation_during_transport_keeps_destination_open_and_client_reusable()
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

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new ReadSourceStream(new byte[] {4, 5}))
            };
        });
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient);
        using var cancellation = new CancellationTokenSource();
        using var destination = new ObservedDestinationStream();
        var downloadTask = api.DownloadAsync("blocked", destination, cancellation.Token);

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloadTask.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Empty(destination.ToArray());
            Assert.False(destination.WasDisposed);
            Assert.Equal(1, handler.SendCount);
        }
        finally
        {
            release.TrySetResult(true);
            if (!downloadTask.IsCompleted)
                try { await downloadTask.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch { }
        }

        using var retryDestination = new ObservedDestinationStream();
        var retryResponse = await api.DownloadAsync("retry", retryDestination, CancellationToken.None);
        Assert.True(retryResponse.IsSuccessStatusCode);
        Assert.Equal(new byte[] {4, 5}, retryDestination.ToArray());
        Assert.Equal(2, handler.SendCount);
    }

    [Fact]
    public async Task Destination_write_failure_propagates_and_does_not_dispose_destination_or_client()
    {
        var source = new ReadSourceStream(new byte[] {1, 2, 3});
        var retrySource = new ReadSourceStream(new byte[] {8, 9});
        var callbackCount = 0;
        var handler = new AsyncFakeHandler((_, _) => Task.FromResult(++callbackCount == 1
            ? new HttpResponseMessage(HttpStatusCode.OK) {Content = new StreamContent(source)}
            : new HttpResponseMessage(HttpStatusCode.OK) {Content = new StreamContent(retrySource)}));
        using var httpClient = new HttpClient(handler);
        var api = ClientApi.FromHttpClient("https://api.example.com/", httpClient);
        using var destination = new ObservedDestinationStream {FailWrites = true};

        await Assert.ThrowsAsync<IOException>(() => api.DownloadAsync("write-fails", destination, CancellationToken.None));

        Assert.True(source.WasDisposed);
        Assert.False(destination.WasDisposed);
        using var retryDestination = new ObservedDestinationStream();
        var response = await api.DownloadAsync("retry", retryDestination, CancellationToken.None);
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(new byte[] {8, 9}, retryDestination.ToArray());
        Assert.Equal(2, handler.SendCount);
    }
}