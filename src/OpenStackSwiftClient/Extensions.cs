using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;

namespace OpenStackSwiftClient
{
  public static class Extensions
  {
    public static string GetValueOrNull(this HttpResponseHeaders headers, string name) {
      return headers.TryGetValues(name, out var s) ? s.FirstOrDefault() : null;
    }
  }
}