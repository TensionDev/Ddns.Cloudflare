using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TensionDev.Ddns.Cloudflare.Configuration;

namespace TensionDev.Ddns.Cloudflare.Tests
{
     /// <summary>
     /// Verifies the background-service execution model from ADR-0002: the worker honours
     /// the host cancellation token, repeats its reconciliation cycle at the configured
     /// interval, and shuts down gracefully. The per-cycle reconciliation is a placeholder
     /// that ADR-0003/ADR-0004 will fill in, so these tests assert lifecycle behaviour
     /// rather than DNS-specific outcomes.
     /// </summary>
    public class WorkerTests
     {
         [Fact]
        public async Task Shuts_down_gracefully_when_shutdown_is_requested()
         {
             var worker = CreateWorker(interval: TimeSpan.FromHours(1));

             await worker.StartAsync(CancellationToken.None);

             var stop = worker.StopAsync(CancellationToken.None);
             Assert.True(await CompletedWithinAsync(stop, TimeSpan.FromSeconds(5)),
                 "The worker did not stop within the expected time after a shutdown request.");
             await stop;
         }

         [Fact]
        public async Task Repeats_reconciliation_cycles_at_the_configured_interval()
         {
             var logger = new CapturingLogger<Worker>();
             var worker = CreateWorker(interval: TimeSpan.FromMilliseconds(10), logger: logger);

             await worker.StartAsync(CancellationToken.None);
             await Task.Delay(150);

             var stop = worker.StopAsync(CancellationToken.None);
             Assert.True(await CompletedWithinAsync(stop, TimeSpan.FromSeconds(5)),
                 "The worker did not stop within the expected time after a shutdown request.");
             await stop;

             Assert.True(logger.MessageCount("Reconciliation cycle started") >= 2,
                 "Expected the worker to run multiple reconciliation cycles at the short configured interval.");
         }

         [Fact]
        public async Task Waits_for_the_configured_interval_before_repeating()
         {
             var logger = new CapturingLogger<Worker>();
             var worker = CreateWorker(interval: TimeSpan.FromHours(1), logger: logger);

             await worker.StartAsync(CancellationToken.None);

             // Let the first cycle run and enter the long interval wait.
             await Task.Delay(100);

             var stop = worker.StopAsync(CancellationToken.None);
             Assert.True(await CompletedWithinAsync(stop, TimeSpan.FromSeconds(5)),
                 "The worker did not stop within the expected time after a shutdown request.");
             await stop;

             // With a one-hour interval the worker is still in its first wait, so only one
             // cycle has started: the interval paces execution.
             Assert.Equal(1, logger.MessageCount("Reconciliation cycle started"));
         }

        private static Worker CreateWorker(TimeSpan interval, ILogger<Worker>? logger = null)
         {
             var options = Options.Create(new DdnsOptions
              {
                 CheckInterval = interval,
                 Cloudflare = new CloudflareOptions
                  {
                     ApiToken = "test-token",
                     Zone = "example.com",
                     Records = new() { "home" },
                  },
              });

             return new Worker(options, logger ?? NullLogger<Worker>.Instance);
         }

        private static async Task<bool> CompletedWithinAsync(Task task, TimeSpan timeout)
         {
             using var cts = new CancellationTokenSource(timeout);
             await Task.WhenAny(task, cts.Task);
             return task.IsCompleted;
         }
     }
}
