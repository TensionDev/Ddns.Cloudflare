# Architecture

## Overview

`TensionDev.Ddns.Cloudflare` is a small .NET Worker Service packaged as
a Linux Docker container.

Its purpose is to keep one or more configured Cloudflare DNS records
aligned with the public IPv4 address of the network where the container
is running.

The application is intentionally stateless. Cloudflare is the source of
truth for DNS state, while the public-IP service is the source of truth
for the externally observed address.

## Runtime Flow

``` text
                    ┌─────────────────────┐
                    │   Runtime Config    │
                    │ Environment Vars    │
                    └──────────┬──────────┘
                               │
                               ▼
┌──────────────┐       ┌─────────────────────┐
│ Public IP    │──────▶│ DDNS Worker Service │
│ Service      │       │                     │
└──────────────┘       └──────────┬──────────┘
                                  │
                                  ▼
                         ┌─────────────────┐
                         │ Cloudflare API  │
                         └────────┬────────┘
                                  │
                                  ▼
                         ┌─────────────────┐
                         │ DNS A Records   │
                         └─────────────────┘
```

## Components

### Worker

The worker owns the application lifecycle and periodic reconciliation.

Responsibilities:

-   Coordinate a reconciliation cycle.
-   Honour the configured check interval.
-   Handle cancellation and graceful shutdown.
-   Log operational events.

The worker should not contain Cloudflare API details or public-IP HTTP
implementation details.

`Worker` delegates each cycle to `DdnsReconciler`, which obtains one public
IPv4 address and reads and conditionally updates each configured record.
`IPublicIpProvider` is resolved once at startup from `PUBLIC_IP_PROVIDER`.
Missing or unknown names select Cloudflare; unknown names produce a warning.
`CloudflarePublicIpProvider` uses `PUBLIC_IP_CLOUDFLARE_URL` and parses
Cloudflare trace output. Its endpoint is supplied by configuration, with no
fallback URL in code. Future services register separate provider classes and
settings under explicit names. Lookup failures do not change the selection.
`CloudflareDnsClient` owns zone and record
discovery and content-only PATCH requests. Named HTTP clients isolate
Cloudflare authentication from public-IP detection. The worker waits after
each cycle completes, retries failed cycles, and propagates host cancellation
through every operation. No state is cached across cycles.

### Public IP Provider

The public-IP provider determines the IPv4 address visible to the
Internet.

Responsibilities:

-   Query an external public-IP service.
-   Validate the returned address.
-   Return the detected public IPv4 address.
-   Report failures without producing an invalid address.

The provider is isolated from the reconciliation process so that the
detection mechanism can be replaced later.

### Cloudflare Integration

The Cloudflare integration communicates with the Cloudflare API.

Responsibilities:

-   Authenticate using the configured API token.
-   Locate configured DNS records.
-   Read existing record values.
-   Update records when required.
-   Surface API failures to the reconciliation process.

### Configuration

Configuration is supplied through environment variables.

Sensitive values must not be logged.

The application does not persist configuration.

## Reconciliation

Each cycle follows this general process:

``` text
Start cycle
    │
    ▼
Get public IPv4
    │
    ├── Failure ──▶ Log failure ──▶ Wait
    │
    ▼
For each configured DNS record
    │
    ▼
Read current Cloudflare value
    │
    ├── Same ──▶ No update
    │
    └── Different ──▶ Update record
    │
    ▼
Wait for configured interval
    │
    └──────────────▶ Start next cycle
```

A failed reconciliation cycle does not terminate the worker unless the
failure represents an unrecoverable startup or configuration error.

## Configuration Boundary

The deployment environment owns configuration.

The application consumes configuration but does not:

-   Generate configuration files.
-   Persist credentials.
-   Store secrets in its image.
-   Provide an interactive configuration mechanism.

## Container

The application targets:

-   .NET 10 LTS
-   Linux containers
-   Dockerfile-based container builds

Native AOT is not required for the initial implementation.

.NET Aspire is not used because the application does not currently
require distributed application orchestration.

## Scope

The initial architecture supports:

-   Cloudflare DNS
-   IPv4
-   `A` records
-   One or more configured records
-   Periodic reconciliation
-   Environment-variable configuration
-   Stateless execution

The following are explicitly outside the initial scope:

-   IPv6 / `AAAA` records
-   Multiple DDNS providers
-   Web UI
-   Persistent database
-   Application-level secret encryption
-   Cloudflare zone or DNS record creation
-   Kubernetes-specific orchestration

These may be considered later when there is a concrete requirement.
