using System;
using System.Text.Json.Serialization;

namespace OpenStackSwiftClient.Models
{
  public class ObjectInfoModel
  {
    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonPropertyName("last_modified")]
    public DateTime LastModified { get; set; }

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; }
  }
}