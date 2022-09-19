using System;
using System.IO;
using System.Linq;

namespace OpenStackSwiftClient.UnitTests.TestUtils
{
  public class RandomStream : Stream
  {
    private readonly long _length;

    private long _totalRead;

    public RandomStream(long length) {
      if (length < 0)
        throw new ArgumentOutOfRangeException(nameof(length));
      _length = length;
    }

    public override void Flush() {
    }

    public override long Seek(long offset, SeekOrigin origin) {
      throw new NotSupportedException();
    }

    public override void SetLength(long value) {
      throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) {
      var toRead = Math.Min(_length - _totalRead, count);
      if (toRead <= 0)
        return 0;
      var random = new Random();
      if (toRead == count && offset == 0 && count == buffer.Length)
        random.NextBytes(buffer);
      else {
        var b = new byte[toRead];
        random.NextBytes(b);
        Array.Copy(b, 0, buffer, offset, (int)toRead);
      }
      _totalRead += toRead;
      return (int)toRead;
    }

    public override void Write(byte[] buffer, int offset, int count) {
      throw new NotSupportedException();
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position {
      get { return _totalRead; }
      set { throw new NotSupportedException(); }
    }
  }
}