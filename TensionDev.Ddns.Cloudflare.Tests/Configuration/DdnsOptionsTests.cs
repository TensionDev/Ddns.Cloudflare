using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TensionDev.Ddns.Cloudflare.Configuration;

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
              .AddInMemoryCollection(settings)
              .Build();
          }

        [Fact]
        public void Binds_all_configured_values()
          {
             var provider = BuildProvider(
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

        [Fact]
        public void Invalid_interval_fails_validation()
          {
             var provider = BuildProvider(
               "CLOUDFLARE_API_TOKEN=secret",
               "CLOUDFLARE_ZONE=example.com",
               "CLOUDFLARE_RECORDS=home",
               "CHECK_INTERVAL=not-a-timespan");

             Assert.Throws<OptionsValidationException>(
               () => provider.GetRequiredService<IOptions<DdnsOptions>>().Value);
          }
     }
}
