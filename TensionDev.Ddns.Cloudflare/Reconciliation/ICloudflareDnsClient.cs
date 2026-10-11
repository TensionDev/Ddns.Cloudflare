using System.Net;

namespace TensionDev.Ddns.Cloudflare.Reconciliation;

// A Cloudflare-specific boundary, not a general-purpose DDNS provider abstraction.
public interface ICloudflareDnsClient
{
    Task<CloudflareDnsRecord> GetRecordAsync(string zone, string name, CancellationToken cancellationToken);
    Task UpdateAddressAsync(string zone, CloudflareDnsRecord record, IPAddress address, CancellationToken cancellationToken);
}

public sealed record CloudflareDnsRecord(string ZoneId, string Id, IPAddress Address);
