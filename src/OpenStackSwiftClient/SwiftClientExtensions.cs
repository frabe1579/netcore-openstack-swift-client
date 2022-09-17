using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using OpenStackSwiftClient.Models;

namespace OpenStackSwiftClient
{
  public static class SwiftClientExtensions
  {
    public static async IAsyncEnumerable<ObjectInfoModel> EnumerateObjectsAsync(this ISwiftClient client, string containerName, string path = null, int pageSize = 1000, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
      ObjectInfoModel[] objs;
      string marker = null;
      do {
        objs = await client.BrowseObjectsAsync(containerName, path, marker, pageSize, cancellationToken).ConfigureAwait(false);
        if (objs.Length == 0)
          yield break;

        foreach (var obj in objs)
          yield return obj;

        marker = objs[objs.Length - 1].Name;
      }
      while (true);
    }

    public static async IAsyncEnumerable<ContainerInfoModel> EnumerateContainersAsync(this ISwiftClient client, int pageSize = 1000, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
      ContainerInfoModel[] objs;
      string marker = null;
      do {
        objs = await client.BrowseContainersAsync(marker, pageSize, cancellationToken).ConfigureAwait(false);
        if (objs.Length == 0)
          yield break;

        foreach (var obj in objs)
          yield return obj;

        marker = objs[objs.Length - 1].Name;
      }
      while (true);
    }
  }
}