using System.Threading;
using System.Threading.Tasks;

namespace OpenStackSwiftClient
{
  public interface ISwiftTempUrlService
  {
    Task<string> CreateGetTempUrlAsync(string containerName, string objectName, string fileName = null, bool inline = false, long deleteAfterSeconds = 86400, CancellationToken cancellationToken = default);
    Task<string> CreatePutTempUrlAsync(string containerName, string objectName, long validForSeconds = 86400, CancellationToken cancellationToken = default);
  }
}