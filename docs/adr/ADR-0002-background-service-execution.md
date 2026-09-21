# ADR-0002: Background Service Execution

-   **Status:** Accepted
-   **Date:** 2026-09-21

## Context

The application is intended to run continuously in a container and
periodically reconcile a public IP address with one or more Cloudflare
DNS records.

The workload does not require an HTTP server, interactive console, or
persistent application state.

## Decision

The application will use the **.NET Worker Service / `BackgroundService`
hosting model**.

The service will:

1.  Start and load runtime configuration.
2.  Determine the current public IP address.
3.  Determine the current value of each managed DNS record.
4.  Update records when their value does not match the current public
    IP.
5.  Wait for the configured interval.
6.  Repeat until the host requests shutdown.

The worker will honour the host cancellation token and shut down
gracefully.

The application will not persist its own state between executions. The
authoritative state is the current public IP and the DNS records held by
Cloudflare.

## Consequences

### Positive

-   The application matches the intended long-running workload.
-   .NET hosting provides dependency injection, configuration, logging,
    cancellation, and lifecycle management.
-   The container can remain stateless.
-   There is no need to expose an HTTP endpoint merely to keep the
    process alive.

### Negative

-   The application depends on a continuously running process.
-   Temporary failures must be handled without terminating the service
    unnecessarily.
-   The polling interval introduces a delay between a public IP change
    and DNS reconciliation.

## Rejected Alternatives

### Console Application

A traditional console application could implement the same loop, but the
Worker Service model provides the required lifecycle and hosting
infrastructure directly.

### Web Application

An ASP.NET Core or Blazor application would introduce an HTTP/web stack
that is not required for the DDNS workload.

### Scheduled External Jobs

Using an external scheduler would move the reconciliation lifecycle
outside the application and make the container less self-contained.
