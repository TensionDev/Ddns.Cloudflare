namespace TensionDev.Ddns.Cloudflare.Integration;

// Messages must be authored locally: never include credentials or remote response bodies.
public sealed class IntegrationException(string message) : Exception(message);
