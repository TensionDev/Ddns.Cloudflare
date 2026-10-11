using Microsoft.Extensions.Options;
using TensionDev.Ddns.Cloudflare.Integration;

namespace TensionDev.Ddns.Cloudflare.Configuration;

public sealed class PublicIpProviderOptionsValidator(IServiceProviderIsKeyedService registrations) : IValidateOptions<DdnsOptions>
{
    public ValidateOptionsResult Validate(string? name, DdnsOptions options)
    {
        var selected = PublicIpProviderSelection.Resolve(options.PublicIpProvider, registrations);
        if (selected == PublicIpProviderSelection.DefaultProvider
            && options.CloudflarePublicIpUrl is not { Scheme: "https", UserInfo: "", Fragment: "" })
            return ValidateOptionsResult.Fail("PUBLIC_IP_CLOUDFLARE_URL must be an absolute HTTPS URL without embedded credentials or a fragment.");

        return ValidateOptionsResult.Success;
    }
}
