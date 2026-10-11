using TensionDev.Ddns.Cloudflare.Integration;
using TensionDev.Ddns.Cloudflare.Reconciliation;
using TensionDev.Ddns.Cloudflare.Tests.TestSupport;
using static TensionDev.Ddns.Cloudflare.Tests.TestSupport.DdnsTestData;
using System.Text.Json;

namespace TensionDev.Ddns.Cloudflare.Tests.Reconciliation;

public class DdnsReconcilerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Full_cycle_updates_only_changed_records_and_reads_fresh_state_next_cycle()
    {
        var requests = new List<string>();
        var changed = false;
        using var factory = new TestClients(async (request, token) =>
        {
            requests.Add($"{request.Method} {request.RequestUri!.PathAndQuery}");
            if (request.RequestUri.Host == "ip.example.com")
            {
                Assert.Null(request.Headers.Authorization);
                return Response("ip=203.0.113.5\n");
            }
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-secret", request.Headers.Authorization?.Parameter);
            Assert.False(request.Headers.Contains("X-Auth-Key"));
            if (request.RequestUri.AbsolutePath.EndsWith("/zones")) return Response(Zones);
            if (request.Method == HttpMethod.Patch)
            {
                Assert.EndsWith("/zones/zone-id/dns_records/web-id", request.RequestUri.AbsolutePath);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                Assert.Single(body.RootElement.EnumerateObject());
                Assert.Equal("203.0.113.5", body.RootElement.GetProperty("content").GetString());
                changed = true;
                return Response("{\"success\":true,\"result\":{}}");
            }
            Assert.Contains("type=A", request.RequestUri.Query);
            if (request.RequestUri.Query.Contains("name.exact=home.example.com"))
                return Response(Records("home.example.com", "203.0.113.5", "home-id"));
            Assert.Contains("name.exact=web.example.com", request.RequestUri.Query);
            return Response(Records("web.example.com", changed ? "203.0.113.5" : "203.0.113.1", "web-id"));
        });
        var options = Settings("home", "web");
        var reconciler = new DdnsReconciler(options, new CloudflarePublicIpProvider(factory), new CloudflareDnsClient(factory, options));
        await reconciler.ReconcileAsync(Token);
        await reconciler.ReconcileAsync(Token);
        Assert.Equal(2, requests.Count(request => request == "GET /"));
        Assert.Equal(4, requests.Count(request => request.Contains("dns_records?")));
        Assert.Single(requests, request => request.StartsWith("PATCH"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("2001:db8::1")]
    [InlineData("127.0.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.1")]
    [InlineData("169.254.1.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("0xCB.0.113.5")]
    [InlineData("203.0.113.005")]
    public async Task Invalid_public_ip_prevents_all_cloudflare_requests(string text)
    {
        var calls = 0;
        using var factory = new TestClients((request, _) =>
        {
            calls++;
            Assert.Equal("ip.example.com", request.RequestUri!.Host);
            return Task.FromResult(Response("ip=" + text));
        });
        var options = Settings("home");
        var reconciler = new DdnsReconciler(options, new CloudflarePublicIpProvider(factory), new CloudflareDnsClient(factory, options));
        await Assert.ThrowsAsync<IntegrationException>(() => reconciler.ReconcileAsync(Token));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task Public_ip_http_failure_aborts_before_dns(int status)
    {
        using var factory = new TestClients((request, _) =>
        {
            Assert.Equal("ip.example.com", request.RequestUri!.Host);
            return Task.FromResult(Response("remote-secret", status));
        });
        var options = Settings("home");
        var reconciler = new DdnsReconciler(options, new CloudflarePublicIpProvider(factory), new CloudflareDnsClient(factory, options));
        var error = await Assert.ThrowsAsync<IntegrationException>(() => reconciler.ReconcileAsync(Token));
        Assert.DoesNotContain("remote-secret", error.ToString());
    }

    [Theory]
    [InlineData("{\"success\":true,\"result\":[]}")]
    [InlineData("{\"success\":true,\"result\":[{},{}]}")]
    [InlineData("{\"success\":true,\"result\":[{}],\"result_info\":{\"total_count\":2}}")]
    [InlineData("{\"success\":true,\"result\":[{\"id\":\"x\",\"type\":\"AAAA\",\"name\":\"home.example.com\",\"content\":\"::1\"}]}")]
    [InlineData("{\"success\":true,\"result\":[{\"id\":\"x\",\"type\":\"A\",\"name\":\"other.example.com\",\"content\":\"203.0.113.1\"}]}")]
    public async Task Missing_ambiguous_or_unexpected_records_are_never_updated(string body)
    {
        using var factory = new TestClients((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            if (request.RequestUri!.Host == "ip.example.com") return Task.FromResult(Response("ip=203.0.113.5"));
            return Task.FromResult(Response(request.RequestUri.AbsolutePath.EndsWith("/zones") ? Zones : body));
        });
        var options = Settings("home");
        var reconciler = new DdnsReconciler(options, new CloudflarePublicIpProvider(factory), new CloudflareDnsClient(factory, options));
        await Assert.ThrowsAsync<IntegrationException>(() => reconciler.ReconcileAsync(Token));
    }

    [Fact]
    public async Task Update_failure_aborts_cycle_and_is_reported_without_remote_body()
    {
        var patchCount = 0;
        using var factory = new TestClients((request, _) =>
        {
            if (request.RequestUri!.Host == "ip.example.com") return Task.FromResult(Response("ip=203.0.113.5"));
            if (request.Method == HttpMethod.Patch)
            {
                patchCount++;
                return Task.FromResult(Response("test-secret", 503));
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/zones")) return Task.FromResult(Response(Zones));
            Assert.Contains("name.exact=home.example.com", request.RequestUri.Query);
            return Task.FromResult(Response(Records("home.example.com")));
        });
        var options = Settings("home", "web");
        var reconciler = new DdnsReconciler(options, new CloudflarePublicIpProvider(factory), new CloudflareDnsClient(factory, options));
        var error = await Assert.ThrowsAsync<IntegrationException>(() => reconciler.ReconcileAsync(Token));
        Assert.Contains("503", error.Message);
        Assert.DoesNotContain("test-secret", error.ToString());
        Assert.Equal(1, patchCount);
    }

    [Fact]
    public async Task Cancellation_during_update_does_not_start_next_record()
    {
        var updating = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        using var factory = new TestClients(async (request, token) =>
        {
            if (request.RequestUri!.Host == "ip.example.com") return Response("ip=203.0.113.5");
            if (request.Method == HttpMethod.Patch)
            {
                updating.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/zones")) return Response(Zones);
            Assert.Contains("name.exact=home.example.com", request.RequestUri.Query);
            return Response(Records("home.example.com"));
        });
        var options = Settings("home", "web");
        var reconciler = new DdnsReconciler(options, new CloudflarePublicIpProvider(factory), new CloudflareDnsClient(factory, options));
        var cycle = reconciler.ReconcileAsync(cancellation.Token);
        await updating.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cycle.WaitAsync(TimeSpan.FromSeconds(5), Token));
    }
}
