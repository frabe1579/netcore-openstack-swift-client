using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenStackSwiftClient.UnitTests
{
  public class SourceStreamWithUnknownLength : Stream
  {
    readonly Stream _inner;

    public SourceStreamWithUnknownLength(Stream inner) {
      _inner = inner;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position {
      get => throw new NotSupportedException();
      set => throw new NotSupportedException();
    }

    public override void Flush() {
      _inner.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count) {
      return _inner.Read(buffer, offset, count);
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

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
      return _inner.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
      return _inner.WriteAsync(buffer, offset, count, cancellationToken);
    }
  }
}
