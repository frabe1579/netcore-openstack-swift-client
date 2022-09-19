namespace OpenStackSwiftClient.UnitTests.TestUtils
{
  public class SubStreamRead : Stream
  {
    private readonly Stream _stream;
    readonly bool _leaveOpen;
    private readonly long _length;

    private long _position;

    /// <summary>
    /// Creates a sub reader leaving open the inner stream when disposed.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="length"></param>
    public SubStreamRead(Stream stream, long length) : this(stream, length, true) {
    }

    public SubStreamRead(Stream stream, long length, bool leaveOpen) {
      _stream = stream ?? throw new ArgumentNullException(nameof(stream));
      _leaveOpen = leaveOpen;
      try {
        _length = Math.Min(length, _stream.Length - _stream.Position);
      }
      catch {
        _length = length;
      }
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush() {
    }

    public override long Length => _length;

    public override long Position {
      get { return _position; }
      set { throw new NotSupportedException(); }
    }

    public override int Read(byte[] buffer, int offset, int count) {
      var c = Math.Min(count, (int)(_length - _position));
      var read = _stream.Read(buffer, offset, c);
      _position += read;
      return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
      var c = Math.Min(count, (int)(_length - _position));
      var read = await _stream.ReadAsync(buffer, offset, c, cancellationToken);
      _position += read;
      return read;
    }

    public override long Seek(long offset, SeekOrigin origin) {
      throw new NotSupportedException();
    }

    public override void SetLength(long value) {
      throw new InvalidOperationException();
    }

    public override void Write(byte[] buffer, int offset, int count) {
      throw new InvalidOperationException();
    }

    protected override void Dispose(bool disposing) {
      if (disposing) if (!_leaveOpen)
          _stream?.Dispose();
      base.Dispose(disposing);
    }
  }
}
