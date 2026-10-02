using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Vip.RestClient.Tests.Utils.Streams;

internal sealed class ReadSourceStream : Stream
{
    #region Propriedades

    private readonly byte[] _bytes;
    private readonly int _chunkSize;
    private readonly int _blockAfterBytes;
    private readonly TaskCompletionSource<bool> _blockedRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _position;
    private bool _blockEntered;

    public Task BlockedRead => _blockedRead.Task;
    public bool WasDisposed { get; private set; }

    public override bool CanRead => !WasDisposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    #endregion

    #region Construtores

    public ReadSourceStream(byte[] bytes, int chunkSize = 81920, int blockAfterBytes = -1)
    {
        _bytes = bytes;
        _chunkSize = chunkSize;
        _blockAfterBytes = blockAfterBytes;
    }

    #endregion

    #region Métodos Públicos

    public void Release() => _release.TrySetResult(true);

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (WasDisposed) throw new ObjectDisposedException(nameof(ReadSourceStream));
        var amount = Math.Min(Math.Min(count, _chunkSize), _bytes.Length - _position);
        if (amount <= 0) return 0;
        Array.Copy(_bytes, _position, buffer, offset, amount);
        _position += amount;
        return amount;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (WasDisposed) throw new ObjectDisposedException(nameof(ReadSourceStream));
        if (!_blockEntered && _blockAfterBytes >= 0 && _position >= _blockAfterBytes)
        {
            _blockEntered = true;
            _blockedRead.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var amount = Math.Min(Math.Min(buffer.Length, _chunkSize), _bytes.Length - _position);
        if (amount <= 0) return 0;
        _bytes.AsMemory(_position, amount).CopyTo(buffer);
        _position += amount;
        return amount;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }

    #endregion
}