using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OpenStackSwiftClient.Models;

namespace OpenStackSwiftClient
{
  public interface ISwiftClient
  {
    Task CreateContainerAsync(string name, ContainerDetailsModel details = null, CancellationToken cancellationToken = default);
    Task DeleteContainerAsync(string name, CancellationToken cancellationToken = default);
    Task<ContainerDetailsModel> GetContainerInfoAsync(string name, CancellationToken cancellationToken = default);
    Task SetContainerInfoAsync(string name, ContainerDetailsModel model, CancellationToken cancellationToken = default);
    Task<ObjectInfoModel> GetObjectInfoAsync(string containerName, string name, CancellationToken cancellationToken = default);
    Task<ObjectInfoModel> UploadObjectAsync(string containerName, string name, Stream content, string contentType = null, string fileName = null, bool overwrite = true, CancellationToken cancellationToken = default);
    Task<ObjectInfoModel> DownloadObjectAsync(string containerName, string name, Stream targetStream, CancellationToken cancellationToken = default);
    Task<Stream> ReadObjectAsync(string containerName, string name, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> GetAsync(string containerName, string name, Action<HttpRequestMessage> requestConfiguration = null, CancellationToken cancellationToken = default);
    Task CopyObjectAsync(string sourceContainerName, string sourceName, string targetContainerName, string targetName, CancellationToken cancellationToken = default);
    Task<ObjectInfoModel[]> BrowseObjectsAsync(string containerName, string prefix, string marker, int? limit = null, CancellationToken cancellationToken = default);
    Task<ContainerInfoModel[]> BrowseContainersAsync(string marker, int? limit = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteObjectAsync(string containerName, string name, bool throwsExceptionIfNotFound = true, CancellationToken cancellationToken = default);
    Task<string> GetUrlAsync(string containerName, string objectName, CancellationToken cancellationToken = default);
    Task<string> GetUrlAsync(string path, CancellationToken cancellationToken = default);
    Task<string> CreateGetTempUrlAsync(string containerName, string objectName, string fileName = null, bool inline = false, long deleteAfterSeconds = 86400, CancellationToken cancellationToken = default);
    Task<string> CreatePutTempUrlAsync(string containerName, string objectName, long validForSeconds = 86400, CancellationToken cancellationToken = default);
  }
}