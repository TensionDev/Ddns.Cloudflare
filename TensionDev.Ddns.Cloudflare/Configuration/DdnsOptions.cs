namespace TensionDev.Ddns.Cloudflare.Configuration
{
     /// <summary>
     /// Top-level application configuration. Sensitive values are never logged.
     /// </summary>
     public class DdnsOptions
     {
          /// <summary>Cloudflare authentication and DNS record selection.</summary>
          public CloudflareOptions Cloudflare { get; set; } = new();

          /// <summary>
          /// How often the public IP is checked and DNS records are reconciled.
          /// Defaults to one hour when <c>CHECK_INTERVAL</c> is not supplied.
          /// </summary>
          public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(1);
     }
}
