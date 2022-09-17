using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Options;
using OpenStackSwiftClient.Models;
using OpenStackSwiftClient.Utils;

namespace OpenStackSwiftClient.Impl
{
  class SwiftTempUrlService : ISwiftTempUrlService
  {
    const string ValidKeyChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private readonly ISwiftClient _swiftClient;
    readonly TempUrlKeyStore _tempUrlKeyStore;
    private readonly SwiftTempUrlOptions _options;

    public SwiftTempUrlService(ISwiftClient swiftClient, TempUrlKeyStore tempUrlKeyStore, IOptionsSnapshot<OpenStackOptions> options) {
      _swiftClient = swiftClient;
      _tempUrlKeyStore = tempUrlKeyStore;
      _options = options.Value.SwiftTempUrl;
    }

    bool IsValidKey(TempUrlKey key) {
      return key != null && (!_options.AutoGenerateKeys || key.DateCreated == null || key.DateCreated.Value.AddSeconds(_options.KeysMinDuration) > DateTime.UtcNow);
    }

    async Task<string> GetKeyAsync(string containerName, CancellationToken cancellationToken = default) {
      var key = _tempUrlKeyStore.GetKey();

      if (IsValidKey(key))
        return key.Key;

      await _tempUrlKeyStore.Semaphore.WaitAsync(cancellationToken);
      try {
        key = _tempUrlKeyStore.GetKey();
        if (IsValidKey(key))
          return key.Key;

        var info = await _swiftClient.GetContainerInfoAsync(containerName, cancellationToken).ConfigureAwait(false);
        TempUrlKey curKey = null;
        if (!string.IsNullOrWhiteSpace(info.MetaTempUrlKey) && info.MetaTempUrlKeyCreated.HasValue)
          curKey = new TempUrlKey(info.MetaTempUrlKey, info.MetaTempUrlKeyCreated.Value);
        if (curKey != null && IsValidKey(curKey)) {
          _tempUrlKeyStore.SetKey(curKey);
          return curKey.Key;
        }

        if (!_options.AutoGenerateKeys) {
          throw new InvalidOperationException($"Keys not available in container '{containerName}' and 'AutoGenerateKeys' option is disabled.");
        }
        var r2 = new ContainerDetailsModel {
          MetaTempUrlKey2 = info.MetaTempUrlKey,
          MetaTempUrlKey2Created = info.MetaTempUrlKeyCreated,
          MetaTempUrlKey = GenerateUniqueKey(),
          MetaTempUrlKeyCreated = DateTime.UtcNow
        };
        await _swiftClient.SetContainerInfoAsync(containerName, r2, cancellationToken).ConfigureAwait(false);
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
      var fullPath = await _swiftClient.GetUrlAsync(containerName, objectName, cancellationToken).ConfigureAwait(false);
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

    string GenerateUniqueKey() {
      using var rng = RandomNumberGenerator.Create();
      var buffer = new byte[_options.KeyLength];
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