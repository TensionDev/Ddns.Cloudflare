namespace TensionDev.Ddns.Cloudflare.Configuration
{
     /// <summary>
     /// Cloudflare-specific configuration, supplied through the <c>CLOUDFLARE_*</c>
     /// environment variables described in ADR-0001.
     /// </summary>
     public class CloudflareOptions
     {
          /// <summary>
          /// API token used to read and update DNS records. Sensitive: must never be logged.
          /// </summary>
          public string ApiToken { get; set; } = string.Empty;

          /// <summary>
          /// Cloudflare zone (domain) that contains the managed DNS records, e.g. <c>example.com</c>.
          /// </summary>
          public string Zone { get; set; } = string.Empty;

          /// <summary>
          /// Names of the DNS records to reconcile, e.g. <c>home</c> or <c>home.example.com</c>.
          /// </summary>
          public List<string> Records { get; set; } = new();
     }
}
