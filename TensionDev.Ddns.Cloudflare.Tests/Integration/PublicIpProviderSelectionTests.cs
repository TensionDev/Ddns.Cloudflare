using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TensionDev.Ddns.Cloudflare.Integration;
using TensionDev.Ddns.Cloudflare.Reconciliation;
using static TensionDev.Ddns.Cloudflare.Tests.TestSupport.DdnsTestData;

namespace TensionDev.Ddns.Cloudflare.Tests.Integration;

public class PublicIpProviderSelectionTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("cloudflare", false)]
    [InlineData(" CLOUDFLARE ", false)]
    [InlineData("cloudflrae", true)]
    [InlineData("ipify", true)]
    public void Defaults_to_cloudflare_and_warns_only_for_unknown_selection(string? selection, bool warn)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var options = Settings("home");
        options.Value.PublicIpProvider = selection;
        services.AddSingleton(options);
        var logger = new CapturingLogger<CloudflarePublicIpProvider>();
        services.AddSingleton<ILogger<CloudflarePublicIpProvider>>(logger);
        services.AddDdnsRuntime();
        using var provider = services.BuildServiceProvider();
        var actual = provider.GetRequiredService<IPublicIpProvider>();
        Assert.IsType<CloudflarePublicIpProvider>(actual);
        Assert.Same(actual, provider.GetRequiredService<IPublicIpProvider>());
        Assert.Equal(warn ? 1 : 0, logger.MessageCount("Falling back"));
        Assert.Equal(1, logger.MessageCount("Public-IP provider selected: cloudflare"));
        if (warn) Assert.All(logger.Messages, message => Assert.DoesNotContain(selection!, message));
    }

    [Fact]
    public async Task Registered_provider_is_selected_once_and_runtime_failure_does_not_switch()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var options = Settings("home");
        options.Value.PublicIpProvider = " TEST ";
        services.AddSingleton(options);
        services.AddDdnsRuntime();
        var fake = new FailingProvider();
        services.AddKeyedSingleton<IPublicIpProvider>("test", fake);
        using var provider = services.BuildServiceProvider();
        var actual = provider.GetRequiredService<IPublicIpProvider>();
        Assert.Same(fake, actual);
        await Assert.ThrowsAsync<HttpRequestException>(() => actual.GetAddressAsync(TestContext.Current.CancellationToken));
        options.Value.PublicIpProvider = "cloudflare";
        Assert.Same(fake, provider.GetRequiredService<IPublicIpProvider>());
    }

    private sealed class FailingProvider : IPublicIpProvider
    {
        public Task<IPAddress> GetAddressAsync(CancellationToken cancellationToken) => throw new HttpRequestException("Lookup failed");
    }
}
