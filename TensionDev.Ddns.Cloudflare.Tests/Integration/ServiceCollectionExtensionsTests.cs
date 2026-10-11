using TensionDev.Ddns.Cloudflare.Integration;
using TensionDev.Ddns.Cloudflare.Reconciliation;
using TensionDev.Ddns.Cloudflare.Tests.TestSupport;
using static TensionDev.Ddns.Cloudflare.Tests.TestSupport.DdnsTestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TensionDev.Ddns.Cloudflare.Tests.Integration;

public class ServiceCollectionExtensionsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Runtime_registration_resolves_worker_and_uses_configured_endpoint()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var options = Settings("home");
        options.Value.CloudflarePublicIpUrl = new Uri("https://ip.example.com/custom/path?format=text");
        services.AddSingleton(options);
        services.AddDdnsRuntime();
        services.AddHttpClient(CloudflarePublicIpProvider.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new TestHttpMessageHandler((request, _) =>
            {
                Assert.Equal("https://ip.example.com/custom/path?format=text", request.RequestUri!.AbsoluteUri);
                Assert.Null(request.Headers.Authorization);
                return Task.FromResult(Response("ip=203.0.113.5\ncolo=SIN\n"));
            }));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.IsType<Worker>(Assert.Single(provider.GetServices<IHostedService>()));
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var ip = factory.CreateClient(CloudflarePublicIpProvider.ClientName);
        using var dns = factory.CreateClient(CloudflareDnsClient.ClientName);
        Assert.Equal("https://ip.example.com/custom/path?format=text", ip.BaseAddress!.AbsoluteUri);
        Assert.Equal("https://api.cloudflare.com/client/v4/", dns.BaseAddress!.AbsoluteUri);
        Assert.Equal(TimeSpan.FromSeconds(30), dns.Timeout);
        Assert.Null(ip.DefaultRequestHeaders.Authorization);
        var address = await provider.GetRequiredService<IPublicIpProvider>().GetAddressAsync(Token);
        Assert.Equal("203.0.113.5", address.ToString());
    }
}
