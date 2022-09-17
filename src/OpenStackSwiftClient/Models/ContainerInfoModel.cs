using System;
using System.Text.Json.Serialization;

namespace OpenStackSwiftClient.Models
{
  public class ContainerInfoModel
  {
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("last_modified")]
    public DateTime LastModified { get; set; }

    [JsonPropertyName("count")]
    public long Count { get; set; }

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }
  }
}