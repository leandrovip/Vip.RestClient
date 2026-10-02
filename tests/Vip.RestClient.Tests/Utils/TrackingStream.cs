using System.IO;

namespace Vip.RestClient.Tests.Utils;

internal sealed class TrackingStream : MemoryStream
{
    #region Propriedades

    public bool WasDisposed { get; private set; }

    #endregion

    #region Construtores

    public TrackingStream(byte[] bytes) : base(bytes) { }

    #endregion

    #region Métodos

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }

    #endregion
}