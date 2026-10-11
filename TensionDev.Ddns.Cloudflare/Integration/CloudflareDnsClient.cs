using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TensionDev.Ddns.Cloudflare.Configuration;
using TensionDev.Ddns.Cloudflare.Reconciliation;

namespace TensionDev.Ddns.Cloudflare.Integration;

public sealed class CloudflareDnsClient(IHttpClientFactory clients, IOptions<DdnsOptions> options) : ICloudflareDnsClient
{
    public const string ClientName = "Cloudflare";

    public async Task<CloudflareDnsRecord> GetRecordAsync(string zone, string name, CancellationToken cancellationToken)
    {
        zone = NormalizeName(zone);
        var recordName = ResolveRecordName(zone, name);
        using var zones = await SendAsync(HttpMethod.Get,
            $"zones?name={Uri.EscapeDataString(zone)}&per_page=50", null, cancellationToken).ConfigureAwait(false);
        var zoneResult = SingleResult(zones.RootElement, "zone");
        if (!string.Equals(Text(zoneResult, "name"), zone, StringComparison.OrdinalIgnoreCase))
            throw new IntegrationException("Cloudflare returned a different zone than requested.");
        var zoneId = Text(zoneResult, "id");

        using var records = await SendAsync(HttpMethod.Get,
            $"zones/{Uri.EscapeDataString(zoneId)}/dns_records?type=A&name.exact={Uri.EscapeDataString(recordName)}&per_page=2",
            null, cancellationToken).ConfigureAwait(false);
        var record = SingleResult(records.RootElement, "A record");
        if (Text(record, "type") != "A"
            || !string.Equals(Text(record, "name"), recordName, StringComparison.OrdinalIgnoreCase)
            || !IPAddress.TryParse(Text(record, "content"), out var address)
            || address.AddressFamily != AddressFamily.InterNetwork)
            throw new IntegrationException("Cloudflare returned an invalid or unexpected A record.");

        return new CloudflareDnsRecord(zoneId, Text(record, "id"), address);
    }

    public async Task UpdateAddressAsync(string zone, CloudflareDnsRecord record, IPAddress address, CancellationToken cancellationToken)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
            throw new IntegrationException("Only IPv4 A records can be updated.");

        // PATCH only the address: preserve TTL, proxy status, comments, tags, and other settings.
        using var result = await SendAsync(HttpMethod.Patch,
            $"zones/{Uri.EscapeDataString(record.ZoneId)}/dns_records/{Uri.EscapeDataString(record.Id)}",
            new { content = address.ToString() }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var client = clients.CreateClient(ClientName);
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.Cloudflare.ApiToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new IntegrationException($"Cloudflare request failed with HTTP {(int)response.StatusCode}.");

        JsonDocument document;
        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            throw new IntegrationException("Cloudflare returned malformed JSON.");
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("success", out var success)
            || success.ValueKind != JsonValueKind.True)
        {
            document.Dispose();
            throw new IntegrationException("Cloudflare reported an unsuccessful API operation.");
        }
        return document;
    }

    private static JsonElement SingleResult(JsonElement root, string description)
    {
        if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array
            || result.GetArrayLength() != 1
            || (root.TryGetProperty("result_info", out var info)
                && (info.ValueKind != JsonValueKind.Object
                    || (info.TryGetProperty("total_count", out var count)
                        && (count.ValueKind != JsonValueKind.Number || !count.TryGetInt32(out var total) || total != 1)))))
            throw new IntegrationException($"Cloudflare lookup must match exactly one existing {description}.");
        return result[0];
    }

    private static string Text(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new IntegrationException("Cloudflare response is missing a required field.");
        return value.GetString()!;
    }

    private static string NormalizeName(string name) => new IdnMapping().GetAscii(name.Trim().TrimEnd('.')).ToLowerInvariant();

    private static string ResolveRecordName(string zone, string name)
    {
        if (name.Trim() == "@") return zone;
        var normalized = NormalizeName(name);
        if (normalized == zone || normalized.EndsWith('.' + zone, StringComparison.Ordinal)) return normalized;
        if (name.Trim().EndsWith('.'))
            throw new IntegrationException("The configured absolute record name is outside the configured zone.");
        return normalized + '.' + zone;
    }
}
