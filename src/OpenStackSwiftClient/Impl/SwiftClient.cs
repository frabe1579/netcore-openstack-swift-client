using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Options;
using OpenStackSwiftClient;
using OpenStackSwiftClient.Models;
using OpenStackSwiftClient.Utils;
using Polly;

namespace OpenStackSwiftClient.Impl
{
  class SwiftClient : ISwiftClient
  {
    const string ValidKeyChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    public static string EndpointName = "object-store";

    private readonly IOpenStackAuth _auth;
    readonly TempUrlKeyStore _tempUrlKeyStore;
    readonly IOptions<OpenStackOptions> _options;
    readonly AsyncPolicy _retryPolicy;
    readonly AsyncPolicy _noOpPolicy;

    public SwiftClient(IOpenStackAuth auth, TempUrlKeyStore tempUrlKeyStore, IOptions<OpenStackOptions> options) {
      _auth = auth;
      _tempUrlKeyStore = tempUrlKeyStore;
      _options = options;
      _retryPolicy = Policy
        .Handle<OpenStackAuthorizationException>()
        .RetryAsync()
        .WrapAsync(Policy
          .Handle<HttpRequestException>()
          .WaitAndRetryAsync(new[] {
              TimeSpan.FromSeconds(2),
              TimeSpan.FromSeconds(5),
              TimeSpan.FromSeconds(10)
          }));
      _noOpPolicy = Policy.NoOpAsync();
    }

    public void Dispose() {
    }

    async Task<HttpResponseMessage> RunAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
      var client = await _auth.CreateHttpClientAsync(EndpointName, cancellationToken).ConfigureAwait(false);
      var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
      if (response.StatusCode == HttpStatusCode.Unauthorized) {
        response.Dispose();
        throw new OpenStackAuthorizationException();
      }
      return response;
    }

    void CheckValidContainerName(string name) {
      if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
      if (name.IndexOfAny(new[] { '/', '\\' }) >= 0)
        throw new ArgumentException("Invalid name");
    }

    public Task CreateContainerAsync(string name, ContainerDetailsModel details = null, CancellationToken cancellationToken = default) {
      CheckValidContainerName(name);
      return _retryPolicy.ExecuteAsync(async ct => {
        using (var request = new HttpRequestMessage(HttpMethod.Put, name)) {
          request.Content = new StreamContent(Stream.Null);
          if (details != null)
            ApplyContainerDetails(details, request);
          using (var response = await RunAsync(request, ct).ConfigureAwait(false))
            response.EnsureSuccessStatusCode();
        }
      }, cancellationToken);
    }

    public Task DeleteContainerAsync(string name, CancellationToken cancellationToken = default) {
      CheckValidContainerName(name);
      return _retryPolicy.ExecuteAsync(async ct => {
        using (var request = new HttpRequestMessage(HttpMethod.Delete, name)) {
          request.Content = new StreamContent(Stream.Null);
          using (var response = await RunAsync(request, ct).ConfigureAwait(false)) {
            if (response.StatusCode == HttpStatusCode.NotFound)
              return;
            response.EnsureSuccessStatusCode();
          }
        }
      }, cancellationToken);
    }

    public Task<ContainerDetailsModel> GetContainerInfoAsync(string name, CancellationToken cancellationToken = default) {
      CheckValidContainerName(name);
      return _retryPolicy.ExecuteAsync(async ct => {
        using (var request = new HttpRequestMessage(HttpMethod.Get, name)) using (var response = await RunAsync(request, ct).ConfigureAwait(false)) {
          if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
          response.EnsureSuccessStatusCode();
          var c1 = response.Headers.GetValueOrNull("X-Container-Meta-Temp-URL-Key-Created");
          var c2 = response.Headers.GetValueOrNull("X-Container-Meta-Temp-URL-Key-2-Created");
          return new ContainerDetailsModel() {
            ObjectCount = XmlConvert.ToInt64(response.Headers.GetValueOrNull("X-Container-Object-Count") ?? "0"),
            BytesUsed = XmlConvert.ToInt64(response.Headers.GetValueOrNull("X-Container-Bytes-Used") ?? "0"),
            MetaTempUrlKey = response.Headers.GetValueOrNull("X-Container-Meta-Temp-URL-Key"),
            MetaTempUrlKey2 = response.Headers.GetValueOrNull("X-Container-Meta-Temp-URL-Key-2"),
            MetaTempUrlKeyCreated = !string.IsNullOrWhiteSpace(c1) ? DateTime.ParseExact(c1, "R", CultureInfo.InvariantCulture) : null,
            MetaTempUrlKey2Created = !string.IsNullOrWhiteSpace(c2) ? DateTime.ParseExact(c2, "R", CultureInfo.InvariantCulture) : null,
          };
        }
      }, cancellationToken);
    }

    public Task SetContainerInfoAsync(string containerName, ContainerDetailsModel model, CancellationToken cancellationToken = default) {
      if (model == null) throw new ArgumentNullException(nameof(model));
      CheckValidContainerName(containerName);
      return _retryPolicy.ExecuteAsync(async ct => {
        using (var request = new HttpRequestMessage(HttpMethod.Post, containerName)) {
          request.Content = new StreamContent(Stream.Null);
          ApplyContainerDetails(model, request);
          using (var response = await RunAsync(request, ct).ConfigureAwait(false))
            response.EnsureSuccessStatusCode();
        }
      }, cancellationToken);
    }

    private static void ApplyContainerDetails(ContainerDetailsModel model, HttpRequestMessage request) {
      if (model.MetaTempUrlKey != null)
        request.Headers.Add("X-Container-Meta-Temp-URL-Key", model.MetaTempUrlKey);
      if (model.MetaTempUrlKeyCreated != null)
        request.Headers.Add("X-Container-Meta-Temp-URL-Key-Created", model.MetaTempUrlKeyCreated.Value.ToString("R", CultureInfo.InvariantCulture));
      if (model.MetaTempUrlKey2 != null)
        request.Headers.Add("X-Container-Meta-Temp-URL-Key-2", model.MetaTempUrlKey2);
      if (model.MetaTempUrlKey2Created != null)
        request.Headers.Add("X-Container-Meta-Temp-URL-Key-2-Created", model.MetaTempUrlKey2Created.Value.ToString("R", CultureInfo.InvariantCulture));
    }

    string GetPath(params string[] parts) {
      return GetPath((IEnumerable<string>)parts);
    }

    string GetPath(IEnumerable<string> parts) {
      return string.Join("/", parts.Select(x => x.Trim('/')));
    }

    public async Task<string> GetUrlAsync(string containerName, string objectName, CancellationToken cancellationToken = default) {
      var url = (await _auth.GetEndpointUrlAsync(EndpointName, cancellationToken).ConfigureAwait(false)).ToString();

      if (!url.EndsWith("/", StringComparison.OrdinalIgnoreCase))
        url += "/";
      if (objectName.StartsWith("/", StringComparison.OrdinalIgnoreCase))
        objectName = objectName.Substring(1);

      return url + containerName + "/" + objectName;
    }

    public async Task<string> GetUrlAsync(string path, CancellationToken cancellationToken = default) {
      var url = (await _auth.GetEndpointUrlAsync(EndpointName, cancellationToken).ConfigureAwait(false)).ToString();

      if (!url.EndsWith("/", StringComparison.OrdinalIgnoreCase))
        url += "/";
      if (path.StartsWith("/", StringComparison.OrdinalIgnoreCase))
        path = path.Substring(1);

      return url + path;
    }

    public Task<ObjectInfoModel> GetObjectInfoAsync(string containerName, string name, CancellationToken cancellationToken = default) {
      if (string.IsNullOrWhiteSpace(containerName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(containerName));
      if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));

      return _retryPolicy.ExecuteAsync(async ct => {
        using (var request = new HttpRequestMessage(HttpMethod.Head, GetPath(containerName, name)))
        using (var response = await RunAsync(request, ct).ConfigureAwait(false)) {
          if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
          response.EnsureSuccessStatusCode();
          return new ObjectInfoModel() {
            Name = name,
            Bytes = response.Content.Headers.ContentLength ?? 0,
            ContentType = response.Content.Headers.ContentType?.MediaType,
            Hash = response.Headers.GetValueOrNull("ETag"),
            LastModified = response.Content.Headers.LastModified.Value.UtcDateTime
          };
        }
      }, cancellationToken);
    }

    public async Task<ObjectInfoModel> UploadObjectAsync(string containerName, string name, Stream content, string contentType = null, string fileName = null, bool overwrite = true, CancellationToken cancellationToken = default) {
      if (content == null) throw new ArgumentNullException(nameof(content));
      if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
      if (string.IsNullOrWhiteSpace(containerName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(containerName));
      using var request = new HttpRequestMessage(HttpMethod.Put, GetPath(containerName, name));
      if (!overwrite) {
        request.Headers.TryAddWithoutValidation("If-None-Match", "*");
        request.Headers.Expect.Add(new NameValueWithParametersHeaderValue("100-Continue"));
      }

      var countingStream = new CountingStream(new IsolatedStream(content));
      request.Content = new StreamContent(countingStream);
      if (!string.IsNullOrWhiteSpace(contentType))
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
      if (!string.IsNullOrWhiteSpace(fileName))
        request.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = fileName };
      using (var response = await RunAsync(request, cancellationToken).ConfigureAwait(false)) {
        if (response.StatusCode == HttpStatusCode.PreconditionFailed)
          throw new ObjectAlreadyExistsException();
        response.EnsureSuccessStatusCode();
        var targetMd5 = response.Headers.GetValueOrNull("ETag")?.Trim('\"');
        var lastModified = response.Content.Headers.LastModified;
        return new ObjectInfoModel {
          Name = name,
          ContentType = contentType,
          Hash = targetMd5,
          LastModified = lastModified?.UtcDateTime ?? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
          Bytes = countingStream.TotalRead
        };
      };
    }

    public Task<ObjectInfoModel> DownloadObjectAsync(string containerName, string name, Stream targetStream, CancellationToken cancellationToken = default) {
      if (targetStream == null) throw new ArgumentNullException(nameof(targetStream));
      if (string.IsNullOrWhiteSpace(containerName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(containerName));
      if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));

      var rc = 0;
      var policy = targetStream.CanSeek ? _retryPolicy : _noOpPolicy;
      return policy.ExecuteAsync(async ct => {
        if (rc > 0) {
          targetStream.Seek(0, SeekOrigin.Begin);
          targetStream.SetLength(0);
        }
        rc++;
        using (var request = new HttpRequestMessage(HttpMethod.Get, GetPath(containerName, name)))
        /*DevSkim: ignore DS126858*/
        using (var response = await RunAsync(request, ct).ConfigureAwait(false)) {
          if (response.StatusCode == HttpStatusCode.NotFound)
            throw new ObjectNotFoundException();
          response.EnsureSuccessStatusCode();
          using (var sourceStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await sourceStream.CopyToAsync(targetStream, 81920, ct).ConfigureAwait(false);
          var sourceMd5 = response.Headers.GetValueOrNull("ETag")?.Trim('\"');
          var r = new ObjectInfoModel {
            Name = name,
            Hash = sourceMd5,
            Bytes = response.Content.Headers.ContentLength.Value,
            ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            LastModified = response.Content.Headers.LastModified.Value.UtcDateTime
          };
          return r;
        }
      }, cancellationToken);
    }

    public async Task<Stream> ReadObjectAsync(string containerName, string name, CancellationToken cancellationToken = default) {
      if (string.IsNullOrWhiteSpace(containerName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(containerName));
      if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));

      return await _retryPolicy.ExecuteAsync(async ct => {
        var request = new HttpRequestMessage(HttpMethod.Get, GetPath(containerName, name));
        HttpResponseMessage response = null;
        try {
          response = await RunAsync(request, ct).ConfigureAwait(false);
          if (response.StatusCode == HttpStatusCode.NotFound)
            throw new ObjectNotFoundException();
          response.EnsureSuccessStatusCode();
          return new StreamWithDisposables(await response.Content.ReadAsStreamAsync().ConfigureAwait(false), response.Content.Headers.ContentLength.Value, request, response);
        }
        catch {
          response?.Dispose();
          request.Dispose();
          throw;
        }
      }, cancellationToken).ConfigureAwait(false);
    }

    public Task<HttpResponseMessage> GetAsync(string containerName, string name, Action<HttpRequestMessage> requestConfiguration = null, CancellationToken cancellationToken = default) {
      if (string.IsNullOrWhiteSpace(containerName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(containerName));
      if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));

      var request = new HttpRequestMessage(HttpMethod.Get, GetPath(containerName, name));
      requestConfiguration?.Invoke(request);
      return RunAsync(request, cancellationToken);
    }

    public Task<ObjectInfoModel[]> BrowseObjectsAsync(string containerName, string path, string marker, int? limit = null, CancellationToken cancellationToken = new CancellationToken()) {
      var query = "?format=json";
      if (!string.IsNullOrWhiteSpace(path))
        query += $"&delimiter={WebUtility.UrlDecode("/")}&prefix={WebUtility.UrlEncode(path)}";
      if (!string.IsNullOrWhiteSpace(marker))
        query += "&marker=" + WebUtility.UrlEncode(marker);
      if (limit != null && limit.Value > 0)
        query += "&limit=" + WebUtility.UrlEncode(XmlConvert.ToString(limit.Value));
      return _retryPolicy.ExecuteAsync(async ct => {
        using (var request = new HttpRequestMessage(HttpMethod.Get, GetPath(containerName) + query)) using (var response = await RunAsync(request, ct).ConfigureAwait(false)) {
          response.EnsureSuccessStatusCode();
          return await response.Content.ReadFromJsonAsync<ObjectInfoModel[]>(cancellationToken: ct).ConfigureAwait(false);
        }
      }, cancellationToken);
    }

    public Task<ContainerInfoModel[]> BrowseContainersAsync(string marker, int? limit = null, CancellationToken cancellationToken = new CancellationToken()) {
      var query = "?format=json";
      if (!string.IsNullOrWhiteSpace(marker))
        query += "&marker=" + WebUtility.UrlEncode(marker);
      if (limit != null && limit.Value > 0)
        query += "&limit=" + WebUtility.UrlEncode(XmlConvert.ToString(limit.Value));
      return _retryPolicy.ExecuteAsync(async ct => {
        using (var request = new HttpRequestMessage(HttpMethod.Get, query)) using (var response = await RunAsync(request, ct).ConfigureAwait(false)) {
          response.EnsureSuccessStatusCode();
          return await response.Content.ReadFromJsonAsync<ContainerInfoModel[]>(cancellationToken: ct).ConfigureAwait(false);
        }
      }, cancellationToken);
    }

    public async Task CopyObjectAsync(string sourceContainerName, string sourceName, string targetContainerName, string targetName, CancellationToken cancellationToken = default) {
      if (string.IsNullOrWhiteSpace(sourceContainerName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(sourceContainerName));
      if (string.IsNullOrWhiteSpace(sourceName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(sourceName));
      if (string.IsNullOrWhiteSpace(targetContainerName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(targetContainerName));
      if (string.IsNullOrWhiteSpace(targetName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(targetName));
      CheckValidContainerName(sourceContainerName);
      CheckValidContainerName(targetContainerName);
      using (var request = new HttpRequestMessage(new HttpMethod("COPY"), GetPath(sourceContainerName, sourceName))) {
        request.Content = new StreamContent(Stream.Null);
        request.Headers.Add("Destination", GetPath(targetContainerName, targetName));
        using (var response = await RunAsync(request, cancellationToken).ConfigureAwait(false)) {
          if (response.StatusCode == HttpStatusCode.NotFound)
            throw new ObjectNotFoundException();
          response.EnsureSuccessStatusCode();
        }
      }
    }

    public async Task<bool> DeleteObjectAsync(string containerName, string name, bool throwsExceptionIfNotFound = true, CancellationToken cancellationToken = default) {
      if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
      CheckValidContainerName(containerName);
      using (var request = new HttpRequestMessage(HttpMethod.Delete, GetPath(containerName, name))) {
        request.Content = new StreamContent(Stream.Null);
        using (var response = await RunAsync(request, cancellationToken).ConfigureAwait(false)) {
          if (response.StatusCode == HttpStatusCode.NotFound) {
            if (throwsExceptionIfNotFound)
              throw new ObjectNotFoundException();
            return false;
          }
          response.EnsureSuccessStatusCode();
          return true;
        }
      }
    }

    bool IsValidKey(TempUrlKey key, OpenStackOptions options) {
      return key != null && (!options.SwiftTempUrl.AutoGenerateKeys || key.DateCreated == null || key.DateCreated.Value.AddSeconds(options.SwiftTempUrl.KeysMinDuration) > DateTime.UtcNow);
    }

    async Task<string> GetKeyAsync(string containerName, CancellationToken cancellationToken = default) {
      var key = _tempUrlKeyStore.GetKey();

      var options = _options.Value;

      if (IsValidKey(key, options))
        return key.Key;

      await _tempUrlKeyStore.Semaphore.WaitAsync(cancellationToken);
      try {
        key = _tempUrlKeyStore.GetKey();
        if (IsValidKey(key, options))
          return key.Key;

        var info = await GetContainerInfoAsync(containerName, cancellationToken).ConfigureAwait(false);
        TempUrlKey curKey = null;
        if (!string.IsNullOrWhiteSpace(info.MetaTempUrlKey) && info.MetaTempUrlKeyCreated.HasValue)
          curKey = new TempUrlKey(info.MetaTempUrlKey, info.MetaTempUrlKeyCreated.Value);
        if (curKey != null && IsValidKey(curKey, options)) {
          _tempUrlKeyStore.SetKey(curKey);
          return curKey.Key;
        }

        if (!options.SwiftTempUrl.AutoGenerateKeys) {
          throw new InvalidOperationException($"Keys not available in container '{containerName}' and 'AutoGenerateKeys' option is disabled.");
        }
        var r2 = new ContainerDetailsModel {
          MetaTempUrlKey2 = info.MetaTempUrlKey,
          MetaTempUrlKey2Created = info.MetaTempUrlKeyCreated,
          MetaTempUrlKey = GenerateUniqueKey(options),
          MetaTempUrlKeyCreated = DateTime.UtcNow
        };
        await SetContainerInfoAsync(containerName, r2, cancellationToken).ConfigureAwait(false);
        var newKey = new TempUrlKey(r2.MetaTempUrlKey, r2.MetaTempUrlKeyCreated);
        _tempUrlKeyStore.SetKey(newKey);
        return newKey.Key;
      }
      finally {
        _tempUrlKeyStore.Semaphore.Release();
      }
    }

    async Task<string> CreateTempUrlAsync(string containerName, string objectName, string method = "GET", string fileName = null, bool inline = false, long deleteAfter = 86400, CancellationToken cancellationToken = default) {
      if (string.IsNullOrWhiteSpace(containerName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(containerName));
      if (string.IsNullOrWhiteSpace(objectName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(objectName));

      var key = await GetKeyAsync(containerName, cancellationToken).ConfigureAwait(false);

      var expiresInUnixTimeSeconds = DateTimeOffset.Now.ToUnixTimeSeconds() + deleteAfter;
      var fullPath = await GetUrlAsync(containerName, objectName, cancellationToken).ConfigureAwait(false);
      var path = new Uri(fullPath).AbsolutePath;
      var hmacBody = $"{method}\n{XmlConvert.ToString(expiresInUnixTimeSeconds)}\n{path}";
      string sig;
      using (var hmac = new HMACSHA1(Encoding.ASCII.GetBytes(key)))
        sig = Hex.ToHex(hmac.ComputeHash(Encoding.UTF8.GetBytes(hmacBody)));

      var s = fullPath + FormattableString.Invariant($"?temp_url_sig={sig}&temp_url_expires={expiresInUnixTimeSeconds}");
      if (!string.IsNullOrWhiteSpace(fileName))
        s += $"&filename={WebUtility.UrlEncode(fileName)}";
      if (inline)
        s += "&inline";
      return s;
    }

    string GenerateUniqueKey(OpenStackOptions options) {
      using var rng = RandomNumberGenerator.Create();
      var buffer = new byte[options.SwiftTempUrl.KeyLength];
      rng.GetBytes(buffer);
      var sb = new StringBuilder(buffer.Length);
      for (int i = 0; i < buffer.Length; i++) {
        sb.Append(ValidKeyChars[buffer[i] % ValidKeyChars.Length]);
      }

      return sb.ToString();
    }

    public Task<string> CreateGetTempUrlAsync(string containerName, string objectName, string fileName = null, bool inline = false, long deleteAfterSeconds = 86400, CancellationToken cancellationToken = default) {
      return CreateTempUrlAsync(containerName, objectName, "GET", fileName, inline, deleteAfterSeconds, cancellationToken);
    }

    public Task<string> CreatePutTempUrlAsync(string containerName, string objectName, long validForSeconds = 86400, CancellationToken cancellationToken = default) {
      return CreateTempUrlAsync(containerName, objectName, "PUT", deleteAfter: validForSeconds, cancellationToken: cancellationToken);
    }
  }
}