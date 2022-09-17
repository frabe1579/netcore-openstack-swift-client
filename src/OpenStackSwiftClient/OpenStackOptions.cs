namespace OpenStackSwiftClient
{
  public class OpenStackOptions
  {
    public string AuthUrl { get; set; }
    public string UserDomainName { get; set; } = "Default";
    public string ProjectId { get; set; }
    public string ProjectName { get; set; }
    public string ProjectDomainName { get; set; } = "Default";
    public string Username { get; set; }
    public string Password { get; set; }
    public string RegionName { get; set; }

    public SwiftTempUrlOptions SwiftTempUrl { get; set; } = new SwiftTempUrlOptions();
  }
}