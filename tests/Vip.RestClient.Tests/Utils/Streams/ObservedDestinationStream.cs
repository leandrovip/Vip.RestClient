using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Vip.RestClient.Tests.Utils.Streams;

internal sealed class ObservedDestinationStream : MemoryStream
{
    #region Propriedades

    public bool FailWrites { get; set; }
    public bool WasDisposed { get; private set; }
    public int FlushCount { get; private set; }

    #endregion

    #region Métodos Públicos

    public override void Flush()
    {
        FlushCount++;
        base.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        FlushCount++;
        return base.FlushAsync(cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (FailWrites) throw new IOException("synthetic destination write failure");
        base.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (FailWrites) throw new IOException("synthetic destination write failure");
        base.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (FailWrites) return Task.FromException(new IOException("synthetic destination write failure"));
        return base.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (FailWrites) return ValueTask.FromException(new IOException("synthetic destination write failure"));
        return base.WriteAsync(buffer, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }

    #endregion
}