using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OpenStackSwiftClient
{
  public interface IOpenStackAuth
  {
    void DropToken(AuthenticationResult token);
    Task<AuthenticationResult> AuthenticateAsync(CancellationToken cancellationToken = default);
    Task<HttpClient> CreateHttpClientAsync(string endpointName, CancellationToken cancellationToken = default);
    Task<Uri> GetEndpointUrlAsync(string endpointName, CancellationToken cancellationToken = default);
  }
}