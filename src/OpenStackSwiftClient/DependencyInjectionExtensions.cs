using System;
using OpenStackSwiftClient;
using OpenStackSwiftClient.Impl;
using OpenStackSwiftClient.Utils;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
  public static class OpenStackDependencyInjectionExtensions
  {
    public static IServiceCollection AddOpenStackSwiftClient(this IServiceCollection services, Action<OpenStackOptions> configure = null) {
      services.AddOptions();
      if (configure != null)
        services.Configure(configure);
      services.AddSingleton<AuthTokenStore>();
      services.AddSingleton<TempUrlKeyStore>();
      services.AddTransient<ISwiftClient, SwiftClient>();
      services.AddTransient<IOpenStackAuth, OpenStackAuth>();
      services.AddTransient<ISwiftTempUrlService, SwiftTempUrlService>();
      return services;
    }
  }
}