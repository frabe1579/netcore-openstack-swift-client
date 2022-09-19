namespace OpenStackSwiftClient.UnitTests.TestUtils
{
  class HashWriteStream : HashStream
  {
    public HashWriteStream(Stream innerStream, HashMode hashMode, bool leaveOpen = false)
      : base(innerStream, hashMode, leaveOpen) {
    }

    public override bool CanRead => false;
    public override bool CanWrite => true;
  }
}