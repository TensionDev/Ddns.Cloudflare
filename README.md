# TensionDev.Ddns.Cloudflare

## Configuration

Configuration is supplied at runtime through environment variables
(see [ADR-0001](docs/adr/ADR-0001-runtime-configuration.md)). The
application does not read a configuration file for these values and does
not persist them.

| Variable             | Required | Sensitive | Description                                                  |
| -------------------- | -------- | --------- | ----------------------------------------------------------- |
| `CLOUDFLARE_API_TOKEN`| Yes   | Yes        | Cloudflare API token with read/edit DNS permissions for the zone. |
| `CLOUDFLARE_ZONE`      | Yes   | No         | Zone (domain) containing the managed records, e.g. `example.com`. |
| `CLOUDFLARE_RECORDS`   | Yes   | No         | Comma-separated record names to manage, e.g. `home,web`.       |
| `CHECK_INTERVAL`       | No    | No         | Reconciliation interval, e.g. `01:00:00`. Defaults to `01:00:00` (one hour). |

-    `CLOUDFLARE_RECORDS` accepts one or more record names separated by
    commas; surrounding whitespace is ignored.
-    `CHECK_INTERVAL` uses the standard .NET time span format
    (`[-][d.]hh:mm:ss[.fffffff]`) and defaults to `01:00:00` (one hour).
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
