using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Vip.RestClient.Tests.Utils.Contents;

internal sealed class FaultingContent : HttpContent
{
    #region Metodos Publicos

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
    {
        return Task.FromException(new InvalidOperationException("synthetic read failure"));
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return true;
    }

    #endregion
}