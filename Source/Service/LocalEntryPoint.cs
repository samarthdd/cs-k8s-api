using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Glasswall.CloudProxy.Api
{
    /// <summary>
    /// The Main function can be used to run the ASP.NET Core application locally using the Kestrel webserver.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class LocalEntryPoint
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseKestrel(options =>
                {
                    Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerLimits limits = options.Limits;
                    limits.MaxRequestBodySize = long.MaxValue;
                })
                .Build();
    }
}
