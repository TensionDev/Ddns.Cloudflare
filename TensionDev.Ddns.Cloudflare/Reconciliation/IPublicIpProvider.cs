using System.Net;

namespace TensionDev.Ddns.Cloudflare.Reconciliation;

public interface IPublicIpProvider
{
    Task<IPAddress> GetAddressAsync(CancellationToken cancellationToken);
}
