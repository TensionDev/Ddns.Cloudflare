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
| `CLOUDFLARE_API_TOKEN`| Yes   | Yes        | Cloudflare API token with read/edit DNS permissions for the zone. |
| `CLOUDFLARE_ZONE`      | Yes   | No         | Zone (domain) containing the managed records, e.g. `example.com`. |
| `CLOUDFLARE_RECORDS`   | Yes   | No         | Comma-separated record names to manage, e.g. `home,web`.       |
| `CHECK_INTERVAL`       | No    | No         | Reconciliation interval, e.g. `01:00:00`. Defaults to `01:00:00` (one hour). |

-    `CLOUDFLARE_RECORDS` accepts one or more record names separated by
    commas; surrounding whitespace is ignored.
-    `CHECK_INTERVAL` uses the standard .NET time span format
    (`[-][d.]hh:mm:ss[.fffffff]`) and defaults to `01:00:00` (one hour)
    when missing or blank. It must be positive and no greater than
    `49.17:02:47.294`, the worker's supported delay limit. Use `00:01:30`
    for 90 seconds; bare numbers follow .NET time span parsing (days).
-    Missing or invalid required configuration terminates the process at
    startup.
-    `CLOUDFLARE_API_TOKEN` is never written to application logs.

### Example

```bash
docker run --rm \
    -e CLOUDFLARE_API_TOKEN=your-restricted-api-token \
    -e CLOUDFLARE_ZONE=example.com \
    -e CLOUDFLARE_RECORDS=home,web \
    -e CHECK_INTERVAL=01:00:00 \
   ghcr.io/tensiondev/ddns-cloudflare:latest
```
