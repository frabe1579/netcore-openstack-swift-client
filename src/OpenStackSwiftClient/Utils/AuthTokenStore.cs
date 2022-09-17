using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace OpenStackSwiftClient.Utils
{
  class AuthTokenStore
  {
    private AuthenticationResult _token;

    public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1);

    public void SetToken(AuthenticationResult token) {
      _token = token;
    }

    public void DropToken(AuthenticationResult token) {
      Interlocked.CompareExchange(ref _token, null, token);
    }

    public AuthenticationResult GetToken() {
      return _token;
    }
  }
}
