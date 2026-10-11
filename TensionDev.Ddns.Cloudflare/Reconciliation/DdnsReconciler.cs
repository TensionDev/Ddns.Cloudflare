using System.Net.Sockets;
using Microsoft.Extensions.Options;
using TensionDev.Ddns.Cloudflare.Configuration;

namespace TensionDev.Ddns.Cloudflare.Reconciliation;

public sealed class DdnsReconciler(
    IOptions<DdnsOptions> options,
    IPublicIpProvider publicIpProvider,
    ICloudflareDnsClient cloudflare) : IDdnsReconciler
{
    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var address = await publicIpProvider.GetAddressAsync(cancellationToken).ConfigureAwait(false);
        if (address is null || address.AddressFamily != AddressFamily.InterNetwork)
            throw new InvalidOperationException("Public-IP detection did not return an IPv4 address.");

        var settings = options.Value.Cloudflare;
        foreach (var name in settings.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = await cloudflare.GetRecordAsync(settings.Zone, name, cancellationToken).ConfigureAwait(false);
            if (!address.Equals(record.Address))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await cloudflare.UpdateAddressAsync(settings.Zone, record, address, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
