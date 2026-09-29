using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TensionDev.Ddns.Cloudflare.Configuration
{
     public static class ServiceCollectionExtensions
     {
          /// <summary>
          /// Registers <see cref="DdnsOptions"/> from the supplied configuration
          /// (environment variables inside a container) and fails fast when required
          /// configuration is missing or invalid, as required by ADR-0001.
          /// </summary>
          public static IServiceCollection AddDdnsOptions(this IServiceCollection services, IConfiguration configuration)
           {
                services.AddOptions<DdnsOptions>(options =>
                 {
                      options.Cloudflare.ApiToken = configuration["CLOUDFLARE_API_TOKEN"] ?? string.Empty;
                      options.Cloudflare.Zone = configuration["CLOUDFLARE_ZONE"] ?? string.Empty;
                      options.Cloudflare.Records = configuration["CLOUDFLARE_RECORDS"]
                       ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       .ToList() ?? new();

                      var rawInterval = configuration["CHECK_INTERVAL"];
                      if (!string.IsNullOrWhiteSpace(rawInterval))
                       {
                            // A supplied-but-invalid interval is a configuration error, so fall
                            // back to zero and let validation below reject it with a clear message.
                            if (!TimeSpan.TryParse(rawInterval, CultureInfo.InvariantCulture, out var interval) || interval <= TimeSpan.Zero)
                             {
                                  options.CheckInterval = TimeSpan.Zero;
                             }
                            else
                             {
                                  options.CheckInterval = interval;
                             }
                       }
                 })
                 .Validate(options => !string.IsNullOrWhiteSpace(options.Cloudflare.ApiToken),
                      "CLOUDFLARE_API_TOKEN must be set to a Cloudflare API token.")
                 .Validate(options => !string.IsNullOrWhiteSpace(options.Cloudflare.Zone),
                      "CLOUDFLARE_ZONE must be set to the Cloudflare zone to manage.")
                 .Validate(options => options.Cloudflare.Records.Count > 0,
                      "At least one DNS record must be configured via CLOUDFLARE_RECORDS.")
                 .Validate(options => options.CheckInterval > TimeSpan.Zero,
                      "CHECK_INTERVAL must be a time span greater than zero (for example 01:00:00).")
                 .ValidateOnStart();

                return services;
           }
     }
}
