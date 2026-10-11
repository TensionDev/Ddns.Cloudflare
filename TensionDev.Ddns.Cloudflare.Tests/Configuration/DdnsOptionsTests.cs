using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using TensionDev.Ddns.Cloudflare.Configuration;
using TensionDev.Ddns.Cloudflare.Reconciliation;

namespace TensionDev.Ddns.Cloudflare.Tests.Configuration
{
    public class DdnsOptionsTests
     {
        private static ServiceProvider BuildProvider(params string[] settings)
          {
             var services = new ServiceCollection();
             services.AddLogging();
             services.AddDdnsOptions(BuildConfiguration(settings));
             return services.BuildServiceProvider();
          }

        private static IConfiguration BuildConfiguration(string[] settings)
          {
             return new ConfigurationBuilder()
              .AddInMemoryCollection(new Dictionary<string, string?> { ["PUBLIC_IP_CLOUDFLARE_URL"] = "https://1.1.1.1/cdn-cgi/trace" })
              .AddInMemoryCollection(settings.Select(setting =>
              {
                  var parts = setting.Split('=', 2);
                  return new KeyValuePair<string, string?>(parts[0], parts[1]);
              }))
              .Build();
          }

        [Fact]
        public void Binds_all_configured_values()
          {
              using var provider = BuildProvider(
               "CLOUDFLARE_API_TOKEN=secret-token",
               "CLOUDFLARE_ZONE=example.com",
               "CLOUDFLARE_RECORDS=home, web , api",
               "CHECK_INTERVAL=00:15:00");

             var options = provider.GetRequiredService<IOptions<DdnsOptions>>().Value;

             Assert.Equal("secret-token", options.Cloudflare.ApiToken);
             Assert.Equal("example.com", options.Cloudflare.Zone);
             Assert.Equal(new[] { "home", "web", "api" }, options.Cloudflare.Records);
             Assert.Equal(TimeSpan.FromMinutes(15), options.CheckInterval);
          }

        [Fact]
        public void Missing_api_token_fails_validation()
          {
             var provider = BuildProvider(
               "CLOUDFLARE_ZONE=example.com",
               "CLOUDFLARE_RECORDS=home");

             var exception = Assert.Throws<OptionsValidationException>(
               () => provider.GetRequiredService<IOptions<DdnsOptions>>().Value);

             Assert.Contains("CLOUDFLARE_API_TOKEN", exception.Message);
          }

        [Fact]
        public void No_records_fails_validation()
          {
             var provider = BuildProvider(
               "CLOUDFLARE_API_TOKEN=secret",
               "CLOUDFLARE_ZONE=example.com");

             Assert.Throws<OptionsValidationException>(
               () => provider.GetRequiredService<IOptions<DdnsOptions>>().Value);
          }

        [Fact]
        public void Missing_interval_defaults_to_one_hour()
          {
             var provider = BuildProvider(
               "CLOUDFLARE_API_TOKEN=secret",
               "CLOUDFLARE_ZONE=example.com",
               "CLOUDFLARE_RECORDS=home");

             var options = provider.GetRequiredService<IOptions<DdnsOptions>>().Value;

             Assert.Equal(TimeSpan.FromHours(1), options.CheckInterval);
          }

        [Theory]
        [InlineData("not-a-timespan")]
        [InlineData("00:00:00")]
        [InlineData("-00:01:00")]
        [InlineData("50.00:00:00")]
        [InlineData("999999999999999999999999")]
        public void Invalid_interval_fails_validation(string interval)
          {
             var provider = BuildProvider(
               "CLOUDFLARE_API_TOKEN=secret",
               "CLOUDFLARE_ZONE=example.com",
               "CLOUDFLARE_RECORDS=home",
                $"CHECK_INTERVAL={interval}");

             Assert.Throws<OptionsValidationException>(
               () => provider.GetRequiredService<IOptions<DdnsOptions>>().Value);
        }

        [Theory]
        [InlineData("CLOUDFLARE_API_TOKEN", "")]
        [InlineData("CLOUDFLARE_API_TOKEN", "   ")]
        [InlineData("CLOUDFLARE_ZONE", "")]
        [InlineData("CLOUDFLARE_ZONE", "   ")]
        [InlineData("CLOUDFLARE_RECORDS", "")]
        [InlineData("CLOUDFLARE_RECORDS", " , , ")]
        [InlineData("PUBLIC_IP_CLOUDFLARE_URL", "")]
        [InlineData("PUBLIC_IP_CLOUDFLARE_URL", null)]
        [InlineData("PUBLIC_IP_CLOUDFLARE_URL", "relative/path")]
        [InlineData("PUBLIC_IP_CLOUDFLARE_URL", "http://example.com/ip")]
        [InlineData("PUBLIC_IP_CLOUDFLARE_URL", "https://user:secret@example.com/ip")]
        [InlineData("PUBLIC_IP_CLOUDFLARE_URL", "https://example.com/ip#fragment")]
        public async Task Invalid_configuration_fails_at_host_start_without_exposing_token(string key, string? value)
        {
            const string token = "secret-token-that-must-not-appear";
            var settings = new Dictionary<string, string?>
            {
                ["CLOUDFLARE_API_TOKEN"] = token,
                ["CLOUDFLARE_ZONE"] = "example.com",
                ["CLOUDFLARE_RECORDS"] = "home",
                ["PUBLIC_IP_CLOUDFLARE_URL"] = "https://1.1.1.1/cdn-cgi/trace",
                [key] = value
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            using var host = new HostBuilder()
                .ConfigureServices(services => services.AddDdnsOptions(configuration))
                .Build();

            var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
            Assert.Contains(key, exception.Message);
            Assert.DoesNotContain(token, exception.ToString());
        }

        [Fact]
        public async Task Valid_configuration_starts_and_worker_logs_do_not_include_token()
        {
            const string token = "unique-secret-token";
            var configuration = BuildConfiguration([
                $"CLOUDFLARE_API_TOKEN={token}",
                "CLOUDFLARE_ZONE=example.com",
                "CLOUDFLARE_RECORDS=home",
                "CHECK_INTERVAL= "
            ]);
            var logger = new CapturingLogger<Worker>();
            var reconciliationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddDdnsOptions(configuration);
                    services.AddSingleton<IDdnsReconciler>(new StubReconciler(_ =>
                    {
                        reconciliationStarted.TrySetResult();
                        return Task.CompletedTask;
                    }));
                    services.AddSingleton(TimeProvider.System);
                    services.AddSingleton<Microsoft.Extensions.Logging.ILogger<Worker>>(logger);
                    services.AddHostedService<Worker>();
                })
                .Build();
            await host.StartAsync(TestContext.Current.CancellationToken);
            try
            {
                // Host startup can complete before the background worker is scheduled.
                // Wait for actual work, which follows the worker's startup log messages.
                await reconciliationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            finally
            {
                await host.StopAsync(TestContext.Current.CancellationToken);
            }

            Assert.Equal(TimeSpan.FromHours(1), host.Services.GetRequiredService<IOptions<DdnsOptions>>().Value.CheckInterval);
            Assert.NotEmpty(logger.Messages);
            Assert.All(logger.Messages, message => Assert.DoesNotContain(token, message));
        }

        [Fact]
        public void Binds_environment_variables()
        {
            // A unique prefix avoids changing real deployment settings or other tests.
            var prefix = $"DDNS_TEST_{Guid.NewGuid():N}_";
            var settings = new Dictionary<string, string>
            {
                ["CLOUDFLARE_API_TOKEN"] = "environment-token",
                ["CLOUDFLARE_ZONE"] = "example.com",
                ["CLOUDFLARE_RECORDS"] = " home, ,web ",
                ["CHECK_INTERVAL"] = "00:01:30",
                ["PUBLIC_IP_CLOUDFLARE_URL"] = "https://ip.example.com/address?format=text",
                ["PUBLIC_IP_PROVIDER"] = " CLOUDFLARE "
            };
            try
            {
                foreach (var setting in settings)
                    Environment.SetEnvironmentVariable(prefix + setting.Key, setting.Value);
                using var configuration = new ConfigurationManager();
                configuration.AddEnvironmentVariables(prefix);
                var services = new ServiceCollection();
                services.AddDdnsOptions(configuration);
                using var provider = services.BuildServiceProvider();
                var options = provider.GetRequiredService<IOptions<DdnsOptions>>().Value;
                Assert.Equal("environment-token", options.Cloudflare.ApiToken);
                Assert.Equal("example.com", options.Cloudflare.Zone);
                Assert.Equal(new[] { "home", "web" }, options.Cloudflare.Records);
                Assert.Equal(TimeSpan.FromSeconds(90), options.CheckInterval);
                Assert.Equal("https://ip.example.com/address?format=text", options.CloudflarePublicIpUrl!.AbsoluteUri);
                Assert.Equal(" CLOUDFLARE ", options.PublicIpProvider);
            }
            finally
            {
                foreach (var key in settings.Keys)
                    Environment.SetEnvironmentVariable(prefix + key, null);
            }
        }
     }
}
