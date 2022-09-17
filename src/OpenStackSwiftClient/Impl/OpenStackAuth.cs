using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using OpenStackSwiftClient.Utils;

namespace OpenStackSwiftClient.Impl
{
  class OpenStackAuth : IOpenStackAuth
  {
    private readonly IOptions<OpenStackOptions> _options;
    readonly AuthTokenStore _tokenStore;
    private readonly IHttpClientFactory _httpClientFactory;

    public OpenStackAuth(IOptions<OpenStackOptions> options, AuthTokenStore tokenStore, IHttpClientFactory httpClientFactory) {
      _options = options;
      _tokenStore = tokenStore;
      _httpClientFactory = httpClientFactory;
    }

    public void DropToken(AuthenticationResult token) {

    }

    bool IsValidToken(AuthenticationResult token) {
      return token != null && (token.ExpiresAt == null || token.ExpiresAt > DateTime.UtcNow);
    }

    public async Task<AuthenticationResult> AuthenticateAsync(CancellationToken cancellationToken = default) {
      var token = _tokenStore.GetToken();

      if (IsValidToken(token))
        return token;

      await _tokenStore.Semaphore.WaitAsync(cancellationToken);
      try {
        token = _tokenStore.GetToken();

        if (IsValidToken(token))
          return token;

        token = await PerformAuthenticationAsync(cancellationToken);
        _tokenStore.SetToken(token);
        return token;
      }
      finally {
        _tokenStore.Semaphore.Release();
      }
    }

    private async Task<AuthenticationResult> PerformAuthenticationAsync(CancellationToken cancellationToken) {
      var options = _options.Value;

      var client = _httpClientFactory.CreateClient();
      var suffix = string.Empty;
      if (!options.AuthUrl.EndsWith("/", StringComparison.OrdinalIgnoreCase))
        suffix += "/";
      client.BaseAddress = new Uri(options.AuthUrl + suffix);
      var response = await client.PostAsync("auth/tokens", new StringContent(JsonSerializer.Serialize(new {
        auth = new {
          identity = new {
            methods = new[] { "password" },
            password = new {
              user = new {
                name = options.Username,
                domain = new {
                  name = options.UserDomainName
                },
                password = options.Password
              }
            }
          },
          scope = new {
            project = new {
              id = options.ProjectId,
              domain = new {
                name = options.ProjectDomainName
              },
              name = options.ProjectName
            }
          }
        }
      }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }), Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);

      response.EnsureSuccessStatusCode();
      var token = response.Headers.GetValueOrNull("X-Subject-Token");
      var info = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));

      var catalogs = info.RootElement.GetProperty("token").GetProperty("catalog");
      var endpoints = new Dictionary<string, string>();
      foreach (var catalog in catalogs.EnumerateArray()) {
        var endpoint = catalog
        .GetProperty("endpoints")
        .EnumerateArray()
        .FirstOrDefault(x => x.GetProperty("region").GetString() == options.RegionName
          && x.GetProperty("interface").GetString() == "public");
        if (endpoint.ValueKind != JsonValueKind.Undefined && endpoint.TryGetProperty("url", out var url))
          endpoints.Add(catalog.GetProperty("type").GetString(), url.GetString().TrimEnd('/') + "/");
      }

      var exp = info.RootElement.GetProperty("token").GetProperty("expires_at").GetDateTime();
      var duration = exp - DateTime.UtcNow;
      if (duration.TotalSeconds > 0) duration = TimeSpan.FromSeconds(duration.TotalSeconds * 0.8);
      else duration = TimeSpan.FromSeconds(3000);
      return new AuthenticationResult(token, endpoints, DateTime.UtcNow + duration);
    }

    public async Task<HttpClient> CreateHttpClientAsync(string endpointName, CancellationToken cancellationToken = default) {
      var authResult = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);

      var client = _httpClientFactory.CreateClient("openstack." + endpointName);
      if (!authResult.Endpoints.TryGetValue(endpointName, out var endpointUrl) && (client.BaseAddress == null || !client.BaseAddress.IsAbsoluteUri))
        throw new Exception($"Invalid endpoint name '{endpointName}'.");
      client.BaseAddress = new Uri(endpointUrl);
      client.DefaultRequestHeaders.Add("X-Auth-Token", authResult.Token);

      return client;
    }

    public async Task<Uri> GetEndpointUrlAsync(string endpointName, CancellationToken cancellationToken = default) {
      var authResult = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
      if (!authResult.Endpoints.TryGetValue(endpointName, out var endpointUrl))
        throw new Exception($"Invalid endpoint name '{endpointName}'.");
      return new Uri(endpointUrl);
    }
  }
}