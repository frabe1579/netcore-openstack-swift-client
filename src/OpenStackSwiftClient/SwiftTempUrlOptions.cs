namespace OpenStackSwiftClient
{
  public class SwiftTempUrlOptions
  {
    public bool AutoGenerateKeys { get; set; } = true;
    public int KeysMinDuration { get; set; } = 86400 * 7;
    public int KeyLength { get; set; } = 40;
    public int CacheDuration { get; set; } = 3600;
  }
}