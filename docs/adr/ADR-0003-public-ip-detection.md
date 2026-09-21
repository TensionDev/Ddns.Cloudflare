# ADR-0003: Public IP Detection

-   **Status:** Accepted
-   **Date:** 2026-09-21

## Context

A DDNS service running inside a private network cannot generally
determine its public IPv4 address from local network interfaces alone.

The service therefore requires an external source that reports the
public address observed by the Internet.

The initial implementation should remain small and should not attempt to
implement NAT discovery itself.

## Decision

The application will obtain its public IP address from an **external
public-IP detection service**.

The public-IP detection mechanism will be represented behind an
application-level abstraction so that the source can be replaced without
changing the DDNS reconciliation logic.

The initial implementation will target **IPv4** and therefore Cloudflare
`A` records.

The application will treat an invalid or unavailable response from the
public-IP service as a failed check and will not modify DNS records for
that iteration.

The selected public-IP service will be configurable or replaceable at
the implementation level rather than making the DDNS reconciliation
logic depend directly on a specific HTTP endpoint.

## Consequences

### Positive

-   Works correctly from networks using NAT.
-   Keeps public-IP discovery separate from Cloudflare-specific
    behaviour.
-   Makes the IP detection mechanism testable.
-   Allows a different public-IP service to be introduced later.

### Negative

-   The service depends on an external endpoint.
-   Public-IP detection can fail independently of Cloudflare.
-   The initial implementation does not manage IPv6 addresses.

## Future Considerations

IPv6 support may be added later using Cloudflare `AAAA` records and an
appropriate public IPv6 detection mechanism.

Multiple public-IP detection providers may also be supported if
reliability or deployment requirements justify it.

The project will not implement its own Internet-facing service solely to
discover the public IP address.
