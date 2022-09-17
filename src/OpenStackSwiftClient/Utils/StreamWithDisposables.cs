using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenStackSwiftClient.Utils
{
  class StreamWithDisposables : Stream
  {
    readonly Stream _inner;

    long? _length;

    private readonly IDisposable[] _disposables;

    public StreamWithDisposables(Stream innerStream, long length, params IDisposable[] disposables) : this(innerStream, disposables) {
      _length = length;
    }

    public StreamWithDisposables(Stream innerStream, params IDisposable[] disposables) {
      _disposables = disposables;
      _inner = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
    }

    public override void Flush() {
      _inner.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken) {
      return _inner.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count) {
      return _inner.Read(buffer, offset, count);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
      return _inner.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin) {
      return _inner.Seek(offset, origin);
    }

    public override void SetLength(long value) {
      _inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count) {
      _inner.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
      return _inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
      return _inner.BeginRead(buffer, offset, count, callback, state);
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) {
      return _inner.BeginWrite(buffer, offset, count, callback, state);
    }

    public override int EndRead(IAsyncResult asyncResult) {
      return _inner.EndRead(asyncResult);
    }

    public override void EndWrite(IAsyncResult asyncResult) {
      _inner.EndWrite(asyncResult);
    }

    public override bool CanTimeout => _inner.CanTimeout;

    public override int ReadByte() {
      return _inner.ReadByte();
    }

    public override int ReadTimeout {
      get => _inner.ReadTimeout;
      set => _inner.ReadTimeout = value;
    }

    public override int WriteTimeout {
      get => _inner.WriteTimeout;
      set => _inner.WriteTimeout = value;
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) {
      return _inner.CopyToAsync(destination, bufferSize, cancellationToken);
    }

    public override void WriteByte(byte value) {
      _inner.WriteByte(value);
    }

    protected override void Dispose(bool disposing) {
      if (disposing) if (_disposables?.Length > 0) for (var i = 0; i < _disposables.Length; i++) _disposables[i]?.Dispose();
      base.Dispose(disposing);
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _length ?? _inner.Length;

    public override long Position {
      get => _inner.Position;
      set => _inner.Position = value;
    }
  }
}