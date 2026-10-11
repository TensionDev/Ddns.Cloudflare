using TensionDev.Ddns.Cloudflare.Configuration;
using TensionDev.Ddns.Cloudflare.Integration;

namespace TensionDev.Ddns.Cloudflare
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddDdnsOptions(builder.Configuration);
            builder.Services.AddDdnsRuntime();

            using var host = builder.Build();
            host.Run();
        }
    }
}
