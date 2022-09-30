using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenStackSwiftClient
{
  class CountingStream : Stream
  {
    Stream innerStream;
    readonly int _intervalMilliseconds;

    long totalRead;
    long totalwritten;

    Action<CountingStream, bool> listener;
    DateTime dtLoop;

    bool _disposed = false;

    public CountingStream(Stream innerStream, int intervalMilliseconds = 200) {
      if (innerStream == null)
        throw new ArgumentNullException(nameof(innerStream));
      this.innerStream = innerStream;
      _intervalMilliseconds = intervalMilliseconds;
    }

    public void AttachListener(Action<CountingStream, bool> listener) {
      this.listener = listener;
    }

    public long TotalRead { get { return Interlocked.Read(ref totalRead); } }
    public long TotalWritten { get { return Interlocked.Read(ref totalwritten); } }

    public override bool CanRead {
      get { return innerStream.CanRead; }
    }

    public override bool CanSeek {
      get { return innerStream.CanSeek; }
    }

    public override bool CanWrite {
      get { return innerStream.CanWrite; }
    }

    public override void Flush() {
      innerStream.Flush();
    }

    public override long Length {
      get { return innerStream.Length; }
    }

    public override long Position {
      get { return innerStream.Position; }
      set { innerStream.Position = value; }
    }

    bool _recursion = false;

    public override int Read(byte[] buffer, int offset, int count) {
      var calculate = true;
      if (!_recursion)
        _recursion = true;
      else
        calculate = false;
      var read = innerStream.Read(buffer, offset, count);
      if (calculate) {
        Interlocked.Add(ref totalRead, read);
        UpdateListener();
        _recursion = false;
      }
      return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
      var calculate = true;
      if (!_recursion)
        _recursion = true;
      else
        calculate = false;
      var read = await base.ReadAsync(buffer, offset, count, cancellationToken);
      if (calculate) {
        Interlocked.Add(ref totalRead, read);
        UpdateListener();
        _recursion = false;
      }
      return read;
    }

    public override long Seek(long offset, SeekOrigin origin) {
      return innerStream.Seek(offset, origin);
    }

    public override void SetLength(long value) {
      innerStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count) {
      var calculate = true;
      if (!_recursion)
        _recursion = true;
      else
        calculate = false;
      innerStream.Write(buffer, offset, count);
      if (calculate) {
        Interlocked.Add(ref totalwritten, count);
        UpdateListener();
        _recursion = false;
      }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
      var calculate = true;
      if (!_recursion)
        _recursion = true;
      else
        calculate = false;
      await base.WriteAsync(buffer, offset, count, cancellationToken);
      if (calculate) {
        Interlocked.Add(ref totalwritten, count);
        UpdateListener();
        _recursion = false;
      }
    }

    private void UpdateListener() {
      if (_intervalMilliseconds == Timeout.Infinite)
        return;
      var listener = this.listener;
      if (listener != null) if (_intervalMilliseconds == 0)
          listener(this, false);
        else {
          var now = DateTime.Now;
          if (dtLoop == DateTime.MinValue || (now - dtLoop).TotalMilliseconds >= _intervalMilliseconds) {
            dtLoop = now;
            listener(this, false);
          }
        }
    }

    protected override void Dispose(bool disposing) {
      if (disposing) {
        innerStream.Dispose();
        if (!_disposed)
          listener?.Invoke(this, true);
      }
      _disposed = true;
      base.Dispose(disposing);
    }
  }
}
