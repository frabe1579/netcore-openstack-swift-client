using System;
using System.Collections.Generic;

namespace OpenStackSwiftClient
{
  public class AuthenticationResult
  {
    public AuthenticationResult(string token, IReadOnlyDictionary<string, string> endpoints, DateTime? expiresAt) {
      Token = token;
      Endpoints = endpoints;
      ExpiresAt = expiresAt;
    }

    public string Token { get; }
    public IReadOnlyDictionary<string, string> Endpoints { get; }
    public DateTime? ExpiresAt { get; }
  }
}