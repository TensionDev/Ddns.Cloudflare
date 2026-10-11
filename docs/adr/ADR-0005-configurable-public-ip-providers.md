# ADR-0005: Configurable Public-IP Providers

-   **Status:** Accepted
-   **Date:** 2026-10-11
-   **Supersedes in part:** [ADR-0003: Public IP Detection](ADR-0003-public-ip-detection.md)

## Context

ADR-0003 separates public-IP detection from DNS reconciliation, but the
implementation preceding this decision used one provider class that accepted multiple
response formats from a configurable endpoint. Changing an endpoint alone
does not adequately separate services with different protocols, response
formats, authentication, or error handling.

Cloudflare is the initial public-IP detection provider. If another service,
such as ipify, is needed later, its communication logic should be implemented
in its own provider class without changing the worker or reconciliation logic.

Deployment configuration must select the provider. Missing, misspelled, or
otherwise unrecognized selections should resolve to Cloudflare.

## Decision

### Provider interface and responsibilities

All public-IP detection providers will implement `IPublicIpProvider`. The
interface will expose an asynchronous operation that accepts a cancellation
token and returns a validated IPv4 address, or reports a failed lookup.

The worker and reconciliation logic will depend only on this interface.
They will not select providers, construct service URLs, or parse responses.

Each service will have a dedicated provider class:

-   `CloudflarePublicIpProvider` will communicate with Cloudflare's trace
    endpoint and interpret its `ip=` field.
-   A future `IpifyPublicIpProvider` will own communication with ipify and
    interpretation of its responses when that service is required.

Each provider owns its request construction, response parsing, and
service-specific error handling. Shared HTTP infrastructure and common IPv4
validation may be reused. Cancellation, timeouts, and secret-safe logging
remain required. Cloudflare DNS credentials must not be sent to public-IP
detection services.

Only the Cloudflare implementation is required initially. This decision
does not require adding ipify or a dynamic plugin-loading system now.

### Configuration and selection

`PUBLIC_IP_PROVIDER` will select a registered provider through the standard
.NET configuration pipeline established in ADR-0001.

Selection will trim surrounding whitespace and compare names without regard
to case. The initial registered name will be `cloudflare`. Future providers
will register explicit names, such as `ipify`.

| Configured selection | Effective provider |
| --- | --- |
| Missing, empty, or whitespace | Cloudflare |
| `cloudflare`, including case or whitespace variations | Cloudflare |
| Another registered provider name | That provider |
| Unknown, misspelled, or not-yet-implemented provider name | Cloudflare |

Dependency-injection registration will resolve the selection once at startup
and supply the effective implementation as `IPublicIpProvider`. Changing the
selection requires restarting the application.

The application will log the effective provider. An unrecognized nonblank
selection will also produce a warning explaining that Cloudflare was selected;
the warning need not echo the raw configuration value.

### Provider-specific endpoint configuration

Endpoints will remain configurable rather than embedded in provider code.
Each provider will have its own endpoint setting, beginning with
`PUBLIC_IP_CLOUDFLARE_URL`. The shipped configuration will set this to
`https://1.1.1.1/cdn-cgi/trace`. A future ipify provider will define its own
setting, such as `PUBLIC_IP_IPIFY_URL`.

This replaces the implementation's shared `PUBLIC_IP_URL` setting when this decision is
implemented. Deployment documentation must describe the migration. The old
setting will not be used to infer the provider or override the Cloudflare
endpoint after an invalid provider selection.

Selection occurs before validating the effective provider's settings. The
selected provider must have a valid absolute HTTPS endpoint without embedded
credentials or a fragment. Missing or invalid settings for that provider
will fail startup. Settings for unselected providers need not be present.

### Failure behavior

Fallback to Cloudflare applies to provider selection only. A timeout,
unavailable service, malformed response, or invalid IPv4 result from a
selected provider remains a failed check. It will not trigger a switch to
another provider, and DNS records will not be modified for that iteration.

If Cloudflare is selected by fallback but its own configuration is invalid,
startup will fail rather than silently substituting a hard-coded endpoint.

### Relationship to existing decisions

This decision strengthens ADR-0003 by requiring service-specific provider
classes and configuration-driven selection, and replaces the implementation's
shared endpoint and multi-format provider approach. ADR-0003's IPv4 scope, external discovery,
and no-DNS-updates-on-detection-failure rules remain in force.

This decision extends ADR-0001's configuration entries with `PUBLIC_IP_PROVIDER`
and provider-specific endpoint settings. Shipped defaults are supplied by
`appsettings.json` and may be overridden through environment variables using
the standard .NET configuration pipeline.

Earlier ADRs remain unchanged as historical decision records. Changes to
their decisions and any supersession relationships are recorded here rather
than by amending those records.

This abstraction concerns public-IP detection. Cloudflare remains the DNS
management provider under ADR-0004; ipify would supply an address, not manage
DNS records. ADR-0004's decision to defer a general DNS-provider abstraction
is unchanged.

## Consequences

### Positive

-   New detection services can be added without changing reconciliation.
-   Each service's protocol and parsing can be tested independently.
-   Deployments can select an implemented provider without rebuilding.
-   Invalid provider selections have a predictable Cloudflare fallback.
-   Provider-specific settings prevent accidental reuse of another service's
    endpoint when falling back.

### Negative

-   Provider registration and separate configuration add some complexity.
-   Fallback may conceal a spelling mistake unless operators notice the
    startup warning and effective-provider log.
-   Existing deployments overriding `PUBLIC_IP_URL` must migrate their setting.
-   Cloudflare configuration must be valid whenever Cloudflare is selected,
    including by fallback.

## Rejected Alternatives

### One Generic Provider with Response-Format Detection

A single class that recognizes every service's response format accumulates
service-specific behavior and weakens the intended abstraction boundary.

### Fail Startup for an Unknown Provider Name

This would make spelling mistakes explicit but conflicts with the required
Cloudflare selection fallback.

### Switch Providers After Runtime Failures

Automatic runtime failover is a separate reliability policy. It is not
required by this decision and would need its own selection and failure rules.
