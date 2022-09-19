using System.Security.Cryptography;

namespace OpenStackSwiftClient.UnitTests.TestUtils
{
  public abstract class HashStream : Stream
  {
    readonly Stream _innerStream;
    private readonly bool _leaveOpen;

    readonly HashAlgorithm _hashAlgorithm;

    public static HashStream CreateRead(Stream innerStream, HashMode hashMode, bool leaveOpen = false) {
      return new HashReadStream(innerStream, hashMode, leaveOpen);
    }

    public static HashStream CreateWrite(Stream innerStream, HashMode hashMode, bool leaveOpen = false) {
      return new HashWriteStream(innerStream, hashMode, leaveOpen);
    }

    internal HashStream(Stream innerStream, HashMode hashMode, bool leaveOpen = false) {
      _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
      _leaveOpen = leaveOpen;
      _hashAlgorithm = Hash.CreateAlgorithm(hashMode);
    }

    public string ComputeHash() {
      _hashAlgorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
      var hash = Hex.ToHex(_hashAlgorithm.Hash);
      _hashAlgorithm.Dispose();
      return hash;
    }

    public override void Flush() {
      _innerStream.Flush();
    }

    public override long Seek(long offset, SeekOrigin origin) {
      throw new InvalidOperationException();
    }

    public override void SetLength(long value) {
      throw new InvalidOperationException();
    }

    public override int Read(byte[] buffer, int offset, int count) {
      if (!CanRead)
        throw new InvalidOperationException("Read not allowed");
      var read = _innerStream.Read(buffer, offset, count);
      if (read > 0)
        _hashAlgorithm.TransformBlock(buffer, offset, read, null, 0);
      return read;
    }

    public override void Write(byte[] buffer, int offset, int count) {
      if (!CanWrite)
        throw new InvalidOperationException("Write not allowed");
      _innerStream.Write(buffer, offset, count);
      _hashAlgorithm.TransformBlock(buffer, offset, count, null, 0);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
      if (!CanRead)
        throw new InvalidOperationException("Read not allowed");
      var read = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
      if (read > 0)
        _hashAlgorithm.TransformBlock(buffer, offset, read, null, 0);
      return read;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
      if (!CanWrite)
        throw new InvalidOperationException("Write not allowed");
      await _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
      _hashAlgorithm.TransformBlock(buffer, offset, count, null, 0);
    }

    public override async Task FlushAsync(CancellationToken cancellationToken) {
      await _innerStream.FlushAsync(cancellationToken);
    }

    public override bool CanSeek => false;
    public override long Length => _innerStream.Length;

    public override long Position {
      get { return _innerStream.Position; }
      set { throw new InvalidOperationException(); }
    }

    protected override void Dispose(bool disposing) {
      base.Dispose(disposing);
      if (disposing)
        if (!_leaveOpen)
          _innerStream.Dispose();
      _hashAlgorithm.Dispose();
    }
  }
}