# ADR-0001: Runtime Configuration

-   **Status:** Accepted
-   **Date:** 2026-09-21

## Context

The Cloudflare DDNS service requires configuration before it can
operate.

At minimum, the application needs to know:

-   How to authenticate with Cloudflare.
-   Which Cloudflare zone should be managed.
-   Which DNS record or records should be managed.
-   How frequently the service should check for changes.

Some configuration values, particularly the Cloudflare API token, are
sensitive and must not be committed to source control or embedded in the
container image.

The application is intended to run as a Docker container, where
environment variables provide a straightforward mechanism for supplying
runtime configuration.

## Decision

The application will use **environment variables as its initial runtime
configuration mechanism**.

The initial configuration will include:

  ------------------------------------------------------------------------
  Configuration            Sensitive               Purpose
  ------------------------ ----------------------- -----------------------
  `CLOUDFLARE_API_TOKEN`   Yes                     Cloudflare API token
                                                   used to manage DNS
                                                   records

  `CLOUDFLARE_ZONE`        No                      Cloudflare zone
                                                   containing the managed
                                                   DNS record or records

  `CLOUDFLARE_RECORDS`     No                      DNS record or records
                                                   to be managed

  `CHECK_INTERVAL`         No                      Frequency at which the
                                                   public IP is checked
  ------------------------------------------------------------------------

The application will use a Cloudflare API token rather than a global
Cloudflare API key.

Sensitive configuration values must not be written to application logs.

Configuration will be supplied by the deployment environment and will
not be persisted by the application.

## Consequences

### Positive

-   Configuration is straightforward for Docker and Docker Compose
    deployments.
-   Secrets are kept outside the container image.
-   Configuration can be changed without rebuilding the application.
-   The application remains stateless.
-   The initial configuration mechanism remains small and predictable.

### Negative

-   Environment variables are not inherently encrypted.
-   Secret exposure depends partly on how the container runtime and host
    are configured.
-   Environment variables may become cumbersome if configuration
    requirements grow significantly.

## Future Considerations

If stronger secret management becomes necessary, the application may
support mechanisms such as:

-   Docker secrets.
-   Container-orchestration secret stores.
-   External secret-management systems.
-   Encrypted configuration.

Application-level encryption will not be introduced unless there is a
demonstrated requirement for it.

## Rejected Alternatives

### Configuration File

A configuration file would be useful for complex configuration but
introduces additional concerns around mounting, distributing, and
securing credentials.

### Command-Line Arguments

Command-line arguments are unsuitable as the primary configuration
mechanism because sensitive values may be exposed through process
inspection.

### Cloudflare API Key

A global Cloudflare API key provides broader access than the application
requires. A restricted API token is preferred.
