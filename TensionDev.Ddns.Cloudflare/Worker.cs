using Microsoft.Extensions.Options;
using TensionDev.Ddns.Cloudflare.Configuration;

namespace TensionDev.Ddns.Cloudflare
{
      /// <summary>
      /// Owns the application lifecycle and periodic reconciliation (ADR-0002).
      /// It coordinates each reconciliation cycle, honours the configured check
      /// interval, tolerates transient failures, and shuts down gracefully.
      /// The public-IP and Cloudflare implementation details live behind the
      /// provider components introduced by ADR-0003 and ADR-0004.
      /// </summary>
     public class Worker(IOptions<DdnsOptions> options, ILogger<Worker> logger) : BackgroundService
      {
        private readonly DdnsOptions _options = options.Value;
        private readonly ILogger<Worker> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
          {
              _logger.LogInformation(
                "DDNS worker started. Checking zone {Zone} for {RecordCount} record(s) every {Interval}.",
                _options.Cloudflare.Zone,
                _options.Cloudflare.Records.Count,
                _options.CheckInterval);

              // The first cycle runs immediately; subsequent cycles run after each
              // interval. A shutdown request may arrive either mid-cycle or during the wait.
              while (!stoppingToken.IsCancellationRequested)
                {
                    try
                      {
                          await ReconcileAsync(stoppingToken).ConfigureAwait(false);
                      }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                      {
                          // A shutdown was requested mid-cycle; stop without retrying.
                          break;
                      }
                    catch (Exception ex)
                      {
                          // A transient failure (for example a public-IP lookup or Cloudflare API
                          // error) must not terminate the service; log it and continue with the
                          // next cycle. Only unrecoverable startup/configuration errors should exit.
                          _logger.LogError(ex, "Reconciliation cycle failed. Will retry after the configured interval.");
                      }

                    try
                      {
                          await Task.Delay(_options.CheckInterval, stoppingToken).ConfigureAwait(false);
                      }
                    catch (OperationCanceledException)
                      {
                          // A shutdown was requested during the wait; stop cleanly.
                          break;
                      }
                }

              _logger.LogInformation("DDNS worker stopped.");
          }

          /// <summary>
          /// Performs a single reconciliation cycle. The public-IP detection
          /// (ADR-0003) and Cloudflare update (ADR-0004) are not implemented yet,
          /// so this is a logging-only placeholder that those ADRs will fill in.
          /// </summary>
         private async Task ReconcileAsync(CancellationToken stoppingToken)
            {
                _logger.LogInformation("Reconciliation cycle started.");

                await Task.CompletedTask;
            }
      }
}
