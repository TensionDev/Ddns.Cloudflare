using TensionDev.Ddns.Cloudflare.Reconciliation;
using TensionDev.Ddns.Cloudflare.Configuration;
using Microsoft.Extensions.Options;

namespace TensionDev.Ddns.Cloudflare.Integration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDdnsRuntime(this IServiceCollection services)
    {
        services.AddHttpClient(CloudflarePublicIpProvider.ClientName, (provider, client) =>
        {
            client.BaseAddress = provider.GetRequiredService<IOptions<DdnsOptions>>().Value.CloudflarePublicIpUrl;
            client.Timeout = TimeSpan.FromSeconds(30);
            client.MaxResponseContentBufferSize = 16 * 1024;
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

        services.AddHttpClient(CloudflareDnsClient.ClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.cloudflare.com/client/v4/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.MaxResponseContentBufferSize = 1024 * 1024;
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
          .RedactLoggedHeaders(["Authorization"]);

        services.AddKeyedSingleton<IPublicIpProvider, CloudflarePublicIpProvider>(PublicIpProviderSelection.DefaultProvider);
        services.AddSingleton<IPublicIpProvider>(provider =>
        {
            var requested = provider.GetRequiredService<IOptions<DdnsOptions>>().Value.PublicIpProvider;
            var selected = PublicIpProviderSelection.Resolve(requested, provider.GetRequiredService<IServiceProviderIsKeyedService>());
            var logger = provider.GetRequiredService<ILogger<CloudflarePublicIpProvider>>();
            if (!string.IsNullOrWhiteSpace(requested) && !string.Equals(requested.Trim(), selected, StringComparison.OrdinalIgnoreCase))
                logger.LogWarning("Unrecognized public-IP provider selection. Falling back to Cloudflare.");
            logger.LogInformation("Public-IP provider selected: {Provider}.", selected);
            return provider.GetRequiredKeyedService<IPublicIpProvider>(selected);
        });
        services.AddSingleton<ICloudflareDnsClient, CloudflareDnsClient>();
        services.AddSingleton<IDdnsReconciler, DdnsReconciler>();
        services.AddSingleton(TimeProvider.System);
        services.AddHostedService<Worker>();
        return services;
    }
}
