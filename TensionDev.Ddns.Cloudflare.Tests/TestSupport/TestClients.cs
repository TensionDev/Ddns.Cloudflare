using TensionDev.Ddns.Cloudflare.Integration;

namespace TensionDev.Ddns.Cloudflare.Tests.TestSupport;

internal sealed class TestClients(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : IHttpClientFactory, IDisposable
{
    private readonly TestHttpMessageHandler _handler = new(send);
    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false)
    {
        BaseAddress = new Uri(name == CloudflarePublicIpProvider.ClientName ? "https://ip.example.com/" : "https://api.cloudflare.com/client/v4/")
    };
    public void Dispose() => _handler.Dispose();
}
