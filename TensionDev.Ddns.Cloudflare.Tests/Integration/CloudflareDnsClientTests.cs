using TensionDev.Ddns.Cloudflare.Integration;
using TensionDev.Ddns.Cloudflare.Reconciliation;
using TensionDev.Ddns.Cloudflare.Tests.TestSupport;
using static TensionDev.Ddns.Cloudflare.Tests.TestSupport.DdnsTestData;

namespace TensionDev.Ddns.Cloudflare.Tests.Integration;

public class CloudflareDnsClientTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("home", "home.example.com")]
    [InlineData("HOME.EXAMPLE.COM.", "home.example.com")]
    [InlineData("@", "example.com")]
    [InlineData("example.com", "example.com")]
    [InlineData("a.b", "a.b.example.com")]
    public async Task Resolves_record_names_within_configured_zone(string configured, string expected)
    {
        using var factory = new TestClients((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/zones")) return Task.FromResult(Response(Zones));
            Assert.Contains($"name.exact={expected}", request.RequestUri.Query);
            return Task.FromResult(Response(Records(expected)));
        });
        var client = new CloudflareDnsClient(factory, Settings(configured));
        var record = await client.GetRecordAsync("EXAMPLE.COM.", configured, Token);
        Assert.Equal("zone-id", record.ZoneId);
        Assert.Equal("record-id", record.Id);
    }

    [Fact]
    public async Task Absolute_record_outside_zone_is_rejected_before_request()
    {
        using var factory = new TestClients((_, _) => throw new InvalidOperationException("Unexpected request"));
        var client = new CloudflareDnsClient(factory, Settings("home.other.com."));
        await Assert.ThrowsAsync<IntegrationException>(() => client.GetRecordAsync("example.com", "home.other.com.", Token));
    }

    [Theory]
    [InlineData(403, "remote-secret")]
    [InlineData(429, "remote-secret")]
    [InlineData(500, "remote-secret")]
    [InlineData(200, "{\"success\":false,\"errors\":[{\"message\":\"remote-secret\"}]}")]
    [InlineData(200, "remote-secret")]
    public async Task Cloudflare_errors_are_sanitized(int status, string body)
    {
        using var factory = new TestClients((_, _) => Task.FromResult(Response(body, status)));
        var client = new CloudflareDnsClient(factory, Settings("home"));
        var error = await Assert.ThrowsAsync<IntegrationException>(() => client.GetRecordAsync("example.com", "home", Token));
        Assert.DoesNotContain("remote-secret", error.ToString());
    }

    [Theory]
    [InlineData("{\"success\":true,\"result\":[]}")]
    [InlineData("{\"success\":true,\"result\":[{},{}]}")]
    [InlineData("{\"success\":true,\"result\":[{\"id\":\"other\",\"name\":\"other.com\"}]}")]
    public async Task Missing_ambiguous_or_wrong_zone_aborts_before_record_lookup(string body)
    {
        var calls = 0;
        using var factory = new TestClients((request, _) =>
        {
            calls++;
            Assert.EndsWith("/zones", request.RequestUri!.AbsolutePath);
            return Task.FromResult(Response(body));
        });
        var client = new CloudflareDnsClient(factory, Settings("home"));
        await Assert.ThrowsAsync<IntegrationException>(() => client.GetRecordAsync("example.com", "home", Token));
        Assert.Equal(1, calls);
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
        Task operation = new CloudflareDnsClient(factory, Settings("home")).GetRecordAsync("example.com", "home", cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5), Token));
    }
}
