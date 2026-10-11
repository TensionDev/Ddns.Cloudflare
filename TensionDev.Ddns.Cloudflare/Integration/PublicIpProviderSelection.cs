using TensionDev.Ddns.Cloudflare.Reconciliation;

namespace TensionDev.Ddns.Cloudflare.Integration;

public static class PublicIpProviderSelection
{
    public const string DefaultProvider = "cloudflare";

    public static string Resolve(string? requested, IServiceProviderIsKeyedService registrations)
    {
        var normalized = requested?.Trim().ToLowerInvariant();
        return !string.IsNullOrEmpty(normalized) && registrations.IsKeyedService(typeof(IPublicIpProvider), normalized)
            ? normalized : DefaultProvider;
    }
}
