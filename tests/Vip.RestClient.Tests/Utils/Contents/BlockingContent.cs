using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vip.RestClient.Tests.Utils.Contents;

internal sealed class BlockingContent : HttpContent
{
    #region Propriedades

    private static readonly byte[] ContentBytes = Encoding.UTF8.GetBytes("not-json");
    private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;
    public bool WasDisposed { get; private set; }

    #endregion

    #region Métodos Públicos

    public void Release() => _release.TrySetResult(true);

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
    {
        return SerializeToStreamAsync(stream, context, CancellationToken.None);
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context, CancellationToken cancellationToken)
    {
        _started.TrySetResult(true);
        await _release.Task.WaitAsync(cancellationToken);
        await stream.WriteAsync(ContentBytes, 0, ContentBytes.Length, cancellationToken);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }

    #endregion
}