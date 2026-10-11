using Microsoft.Extensions.Options;
using TensionDev.Ddns.Cloudflare.Configuration;
using TensionDev.Ddns.Cloudflare.Integration;
using TensionDev.Ddns.Cloudflare.Reconciliation;

namespace TensionDev.Ddns.Cloudflare;

public sealed class Worker(
    IOptions<DdnsOptions> options,
    IDdnsReconciler reconciler,
    TimeProvider timeProvider,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly TimeSpan _interval = options.Value.CheckInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DDNS worker started. Check interval: {Interval}.", _interval);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    logger.LogInformation("Reconciliation cycle started.");
                    await reconciler.ReconcileAsync(stoppingToken).ConfigureAwait(false);
                    logger.LogInformation("Reconciliation cycle completed.");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    // Only locally authored integration messages are safe to log.
                    // Arbitrary exception messages and remote bodies may contain credentials.
                    var reason = exception is IntegrationException ? exception.Message : exception.GetType().Name;
                    logger.LogError("Reconciliation cycle failed ({Reason}). Retrying after {Interval}.", reason, _interval);
                }

                // Wait after completion/failure, so cycles never overlap.
                await Task.Delay(_interval, timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown during the interval wait.
        }
        finally
        {
            logger.LogInformation("DDNS worker stopped.");
        }
    }
}
