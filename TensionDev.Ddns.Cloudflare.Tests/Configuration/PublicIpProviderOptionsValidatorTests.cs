using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TensionDev.Ddns.Cloudflare.Configuration;
using TensionDev.Ddns.Cloudflare.Reconciliation;

namespace TensionDev.Ddns.Cloudflare.Tests.Configuration;

public class PublicIpProviderOptionsValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("cloudflrae")]
    [InlineData("ipify")]
    public async Task Fallback_requires_valid_cloudflare_settings_and_ignores_legacy_url(string? selection)
    {
        using var host = CreateHost(selection, false);
        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        Assert.Contains("PUBLIC_IP_CLOUDFLARE_URL", error.Message);
    }

    [Fact]
    public async Task Unselected_cloudflare_endpoint_is_not_required()
    {
        using var host = CreateHost(" TEST ", true);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static IHost CreateHost(string? selection, bool registerTest)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PUBLIC_IP_PROVIDER"] = selection,
            ["PUBLIC_IP_URL"] = "https://old.example.com/ip",
            ["CLOUDFLARE_API_TOKEN"] = "test",
            ["CLOUDFLARE_ZONE"] = "example.com",
            ["CLOUDFLARE_RECORDS"] = "home"
        }).Build();
        return new HostBuilder().ConfigureServices(services =>
        {
            services.AddDdnsOptions(configuration);
            if (registerTest) services.AddKeyedSingleton<IPublicIpProvider, TestProvider>("test");
        }).Build();
    }

    private sealed class TestProvider : IPublicIpProvider
    {
        public Task<IPAddress> GetAddressAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
