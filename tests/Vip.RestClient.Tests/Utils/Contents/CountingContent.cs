using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Vip.RestClient.Tests.Utils.Contents;

internal sealed class CountingContent : HttpContent
{
    #region Propriedades

    private readonly byte[] _bytes;

    public bool WasRead { get; private set; }

    #endregion

    #region Construtores

    public CountingContent(string value) { _bytes = Encoding.UTF8.GetBytes(value); }

    #endregion

    #region Métodos Públicos

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
    {
        WasRead = true;
        return stream.WriteAsync(_bytes, 0, _bytes.Length);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _bytes.Length;
        return true;
    }

    #endregion
}