# ADR-0004: Cloudflare DNS Integration

-   **Status:** Accepted
-   **Date:** 2026-09-21

## Context

The initial DDNS implementation targets Cloudflare DNS.

The application needs to locate and update the configured DNS record
without requiring broader Cloudflare account access than necessary.

Cloudflare provides an API token mechanism that can be restricted to the
required zone and DNS permissions.

## Decision

The application will integrate with Cloudflare through the **Cloudflare
API**.

Authentication will use a **Cloudflare API token** with the minimum
permissions required to read and edit DNS records for the configured
zone.

The application will manage existing DNS records rather than creating
arbitrary records automatically.

For the initial IPv4 implementation, managed records will be `A`
records.

For each configured record, the service will:

1.  Locate the configured DNS record.
2.  Read its current address.
3.  Compare it with the detected public IPv4 address.
4.  Update the record only when the address differs.

The application will not modify unrelated DNS records.

Cloudflare-specific API handling will remain separate from the worker's
reconciliation loop.

## Consequences

### Positive

-   Access can be restricted to the required Cloudflare zone.
-   Existing DNS records are managed explicitly.
-   Unrelated DNS records are left untouched.
-   Cloudflare API details remain isolated from the worker lifecycle.

### Negative

-   The application is initially tied to Cloudflare.
-   Cloudflare API availability becomes part of the service's runtime
    dependencies.
-   Record discovery and API error handling must be implemented.

## Future Considerations

If another DDNS provider is supported, the provider-specific
implementation may be separated behind a common DDNS provider
abstraction.

The project will not introduce a provider abstraction solely for the
purpose of anticipating hypothetical providers. A second provider should
provide the practical justification for that abstraction.

IPv6 / `AAAA` record support may be added in a later decision.
