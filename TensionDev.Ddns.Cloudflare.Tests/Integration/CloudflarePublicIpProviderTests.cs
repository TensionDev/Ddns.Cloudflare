using TensionDev.Ddns.Cloudflare.Integration;
using TensionDev.Ddns.Cloudflare.Reconciliation;
using TensionDev.Ddns.Cloudflare.Tests.TestSupport;
using static TensionDev.Ddns.Cloudflare.Tests.TestSupport.DdnsTestData;

namespace TensionDev.Ddns.Cloudflare.Tests.Integration;

public class CloudflarePublicIpProviderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("fl=123\nip=203.0.113.5\ncolo=SIN\n")]
    [InlineData("fl=123\r\nip=203.0.113.5\r\nwarp=off\r\n")]
    public async Task Accepts_cloudflare_trace(string body)
    {
        using var factory = new TestClients((request, _) =>
        {
            Assert.Null(request.Headers.Authorization);
            return Task.FromResult(Response(body));
        });
        var result = await new CloudflarePublicIpProvider(factory).GetAddressAsync(Token);
        Assert.Equal("203.0.113.5", result.ToString());
    }

    [Theory]
    [InlineData("colo=SIN\nwarp=off")]
    [InlineData("203.0.113.5\n")]
    [InlineData("ip=203.0.113.5\nip=203.0.113.6")]
    [InlineData("ip=")]
    [InlineData("ip=invalid")]
    [InlineData("ip=2001:db8::1")]
    [InlineData("ip=192.168.1.1")]
    public async Task Rejects_invalid_or_ambiguous_trace(string body)
    {
        using var factory = new TestClients((_, _) => Task.FromResult(Response(body)));
        await Assert.ThrowsAsync<IntegrationException>(() => new CloudflarePublicIpProvider(factory).GetAddressAsync(Token));
    }

    [Fact]
    public async Task Http_requests_honor_cancellation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        using var factory = new TestClients(async (_, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Unreachable");
        });
        Task operation = new CloudflarePublicIpProvider(factory).GetAddressAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5), Token));
    }
}
