# TensionDev.Ddns.Cloudflare

## Configuration

Supply DDNS configuration at runtime through environment variables
as described in [ADR-0001](docs/adr/ADR-0001-runtime-configuration.md).
The application uses the standard .NET Generic Host configuration pipeline
through `builder.Configuration`; its other default providers remain enabled.
Environment variables override values from `appsettings.json`,
`appsettings.{Environment}.json`, and development user secrets. Command-line
arguments override environment variables.

Supply the Cloudflare API token securely through the deployment environment.
Do not commit it to configuration files, embed it in the container image, or
pass it as a command-line argument. The application does not persist
configuration values.

| Variable             | Required | Sensitive | Description                                                  |
| -------------------- | -------- | --------- | ----------------------------------------------------------- |
| `CLOUDFLARE_API_TOKEN`| Yes   | Yes        | Cloudflare API token with Zone Read and DNS Edit permissions, restricted to the managed zone. |
| `CLOUDFLARE_ZONE`      | Yes   | No         | Zone (domain) containing the managed records, e.g. `example.com`. |
| `CLOUDFLARE_RECORDS`   | Yes   | No         | Comma-separated record names to manage, e.g. `home,web`.       |
| `CHECK_INTERVAL`       | No    | No         | Reconciliation interval, e.g. `01:00:00`. Defaults to `01:00:00` (one hour). |
| `PUBLIC_IP_PROVIDER` | No | No | Provider name; defaults to `cloudflare`. Unknown names also fall back to Cloudflare with a warning. |
| `PUBLIC_IP_CLOUDFLARE_URL` | When Cloudflare is selected; supplied by appsettings | No | Cloudflare trace endpoint. Shipped configuration uses `https://1.1.1.1/cdn-cgi/trace`. |

-    `CLOUDFLARE_RECORDS` accepts one or more record names separated by
    commas; surrounding whitespace is ignored. Use `@` for the zone apex,
    a relative name such as `home` or `a.b`, or a full name within the zone
    such as `home.example.com`. A trailing dot makes a name absolute.
-    `CHECK_INTERVAL` uses the standard .NET time span format
    (`[-][d.]hh:mm:ss[.fffffff]`) and defaults to `01:00:00` (one hour)
    when missing or blank. It must be positive and no greater than
    `49.17:02:47.294`, the worker's supported delay limit. Use `00:01:30`
    for 90 seconds; bare numbers follow .NET time span parsing (days).
-    Missing or invalid required configuration terminates the process at
    startup.
-    `CLOUDFLARE_API_TOKEN` is never written to application logs.
-    `PUBLIC_IP_PROVIDER` ignores surrounding whitespace and case. Only
    `cloudflare` is implemented currently; `ipify` therefore falls back to
    Cloudflare. The effective provider is logged once at startup.
-    `PUBLIC_IP_CLOUDFLARE_URL` must return Cloudflare trace
    output containing exactly one `ip=` field. The URL must use HTTPS and
    contain no embedded credentials or fragment. There is no endpoint
    fallback in code: missing or invalid configuration fails at startup.
    Restart the service after changing the setting.

### Example

```bash
docker run --rm \
    -e CLOUDFLARE_API_TOKEN=your-restricted-api-token \
    -e CLOUDFLARE_ZONE=example.com \
    -e CLOUDFLARE_RECORDS=home,web \
    -e CHECK_INTERVAL=01:00:00 \
    -e PUBLIC_IP_PROVIDER=cloudflare \
    -e PUBLIC_IP_CLOUDFLARE_URL=https://1.1.1.1/cdn-cgi/trace \
   ghcr.io/tensiondev/ddns-cloudflare:latest
```

## Runtime behavior

The .NET background worker runs its first reconciliation immediately, then
waits for `CHECK_INTERVAL` after each completed or failed cycle. Cycles run
sequentially and never overlap. Host shutdown cancels both pending HTTP
requests and the interval wait.

Each cycle obtains the public IPv4 address from the selected provider,
resolves the configured Cloudflare zone by name, and reads each configured
`A` record. Only differing addresses are updated, using a content-only
[Cloudflare PATCH request](https://developers.cloudflare.com/api/resources/dns/subresources/records/methods/edit/)
that preserves TTL, proxy status, and other record settings. Records must
already exist and match uniquely; the application does not create records.

The shipped endpoint uses Cloudflare's
[/cdn-cgi/trace diagnostic endpoint](https://developers.cloudflare.com/fundamentals/reference/cdn-cgi-endpoint/).
The IPv4 literal `1.1.1.1` avoids selecting an IPv6 destination. You can
change the URL in configuration without rebuilding. Cloudflare endpoints
must return trace output; plain-text addresses and JSON are not supported
by this provider.
An IPv6 response is rejected and no DNS updates occur for that cycle.

The token needs Zone Read for
[zone lookup](https://developers.cloudflare.com/api/resources/zones/methods/list/)
and DNS Edit for record access and updates, scoped to the managed zone.
Public-IP requests never receive the Cloudflare token. Each HTTP request
has a 30-second timeout.

Invalid or unavailable public-IP detection prevents all DNS operations for
that cycle. A Cloudflare failure stops the remainder of the cycle; earlier
successful updates remain applied. Failures are logged without credentials
or remote response bodies, and the worker retries after the configured
interval. Every cycle reads fresh external state; no application state is
persisted. IPv6 and `AAAA` records are not supported.

Fallback applies only to invalid provider selection. A failed lookup does
not switch providers. Missing or invalid settings for the effective provider
fail startup; settings for unselected providers are not required.

### Migration from PUBLIC_IP_URL

Replace `PUBLIC_IP_URL` with `PUBLIC_IP_CLOUDFLARE_URL` and use a Cloudflare
trace endpoint. The old setting is ignored and cannot override the selected
provider's endpoint. A previous plain-text service requires a dedicated
provider implementation before it can be selected.

### Adding a public-IP provider

Implement `IPublicIpProvider` in a service-specific class and register it as
a keyed singleton using a lowercase name (for example `ipify`). Register its
own HTTP client, endpoint options, and startup validator, validating those
settings only when that provider is selected. The existing selection resolver
discovers registered keys; the worker and reconciler require no changes.
Provider endpoints remain configuration values. See
[ADR-0005](docs/adr/ADR-0005-configurable-public-ip-providers.md).

## Verification

Run the automated tests with:

```bash
dotnet run --project TensionDev.Ddns.Cloudflare.Tests
```

Tests use simulated HTTP responses and a controllable clock; they do not
contact Cloudflare or change live DNS records.
