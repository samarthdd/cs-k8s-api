using Glasswall.CloudProxy.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Glasswall.CloudProxy.IntegrationTest
{
    public class CustomWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
    {
        protected override IHostBuilder CreateHostBuilder()
        {
            var builder = Host.CreateDefaultBuilder()
                              .ConfigureWebHostDefaults(x =>
                              {
                                  x.UseStartup<Startup>().UseTestServer();
                              });
            return builder;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(config =>
            {
                var integrationConfig = new ConfigurationBuilder()
                  .AddJsonFile(Constants.APP_SETTINGS_FILE_NAME)
                  .Build();

                config.AddConfiguration(integrationConfig);
            });
        }
    }
}
