namespace OpenStackSwiftClient.UnitTests.TestUtils
{
  public class IsolatedStream : Stream
  {
    readonly Stream _innerStream;

    public IsolatedStream(Stream innerStream) {
      if (innerStream == null)
        throw new ArgumentNullException(nameof(innerStream));
      _innerStream = innerStream;
    }

    public override void Flush() {
      _innerStream.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken) {
      return _innerStream.FlushAsync(cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin) {
      return _innerStream.Seek(offset, origin);
    }

    public override void SetLength(long value) {
      _innerStream.SetLength(value);
    }

    public override int Read(byte[] buffer, int offset, int count) {
      return _innerStream.Read(buffer, offset, count);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
      return _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count) {
      _innerStream.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
      return _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => _innerStream.CanSeek;
    public override bool CanWrite => _innerStream.CanWrite;
    public override long Length => _innerStream.Length;

    public override long Position {
      get { return _innerStream.Position; }
      set { _innerStream.Position = value; }
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) {
      return _innerStream.CopyToAsync(destination, bufferSize, cancellationToken);
    }
  }
}