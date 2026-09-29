using TensionDev.Ddns.Cloudflare.Configuration;

namespace TensionDev.Ddns.Cloudflare
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddDdnsOptions(builder.Configuration);

            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}
