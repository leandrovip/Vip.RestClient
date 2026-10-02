using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Vip.RestClient.Tests.Utils.Contents;

internal sealed class TrackingContent : HttpContent
{
    #region Propriedades

    public bool WasDisposed { get; private set; }

    #endregion

    #region Metodos

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
    {
        return Task.CompletedTask;
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }

    #endregion
}