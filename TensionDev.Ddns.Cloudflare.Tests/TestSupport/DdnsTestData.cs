using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TensionDev.Ddns.Cloudflare.Configuration;

namespace TensionDev.Ddns.Cloudflare.Tests.TestSupport;

internal static class DdnsTestData
{
    internal const string Zones = "{\"success\":true,\"result\":[{\"id\":\"zone-id\",\"name\":\"example.com\"}],\"result_info\":{\"total_count\":1}}";
    internal static string Records(string name, string address = "203.0.113.1", string id = "record-id") =>
        JsonSerializer.Serialize(new { success = true, result = new[] { new { id, name, type = "A", content = address } } });
    internal static HttpResponseMessage Response(string body, int status = 200) =>
        new((HttpStatusCode)status) { Content = new StringContent(body) };
    internal static IOptions<DdnsOptions> Settings(params string[] records) => Options.Create(new DdnsOptions
    {
        Cloudflare = new CloudflareOptions { ApiToken = "test-secret", Zone = "example.com", Records = records.ToList() }
    });

}
