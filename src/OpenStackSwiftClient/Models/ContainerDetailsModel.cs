using System;
using System.Collections.Generic;

namespace OpenStackSwiftClient.Models
{
  public class ContainerDetailsModel
  {
    public string MetaTempUrlKey { get; set; }
    public string MetaTempUrlKey2 { get; set; }
    public long? ObjectCount { get; set; }
    public long? BytesUsed { get; set; }
    public DateTime? MetaTempUrlKeyCreated { get; set; }
    public DateTime? MetaTempUrlKey2Created { get; set; }
  }
}