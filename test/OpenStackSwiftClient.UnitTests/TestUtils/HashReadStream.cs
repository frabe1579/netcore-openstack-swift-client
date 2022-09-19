namespace OpenStackSwiftClient.UnitTests.TestUtils
{
  class HashReadStream : HashStream
  {
    public HashReadStream(Stream innerStream, HashMode hashMode, bool leaveOpen = false)
      : base(innerStream, hashMode, leaveOpen) {
    }

    public override bool CanRead => true;
    public override bool CanWrite => false;
  }
}