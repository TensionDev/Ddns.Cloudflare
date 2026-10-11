using System.Net;
using System.Net.Sockets;
using TensionDev.Ddns.Cloudflare.Reconciliation;

namespace TensionDev.Ddns.Cloudflare.Integration;

public sealed class CloudflarePublicIpProvider(IHttpClientFactory clients) : IPublicIpProvider
{
    public const string ClientName = "CloudflarePublicIp";

    public async Task<IPAddress> GetAddressAsync(CancellationToken cancellationToken)
    {
        using var client = clients.CreateClient(ClientName);
        using var response = await client.GetAsync("", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new IntegrationException($"Public-IP lookup failed with HTTP {(int)response.StatusCode}.");

        var text = (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Trim();
        var addresses = text.Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("ip=", StringComparison.Ordinal))
            .Select(line => line[3..].Trim()).ToArray();
        if (addresses.Length != 1)
            throw new IntegrationException("Public-IP trace response must contain exactly one ip field.");
        text = addresses[0];
        if (!IPAddress.TryParse(text, out var address)
            || address.AddressFamily != AddressFamily.InterNetwork || address.ToString() != text)
            throw new IntegrationException("Public-IP lookup returned an invalid IPv4 address.");

        var bytes = address.GetAddressBytes();
        if (bytes[0] is 0 or 10 or 127 or >= 224
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127))
            throw new IntegrationException("Public-IP lookup returned a non-public IPv4 address.");

        return address;
    }
}
