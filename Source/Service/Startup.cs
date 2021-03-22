using Glasswall.CloudProxy.Common.Setup;
using Glasswall.CloudProxy.Api.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using System.Diagnostics.CodeAnalysis;

namespace Glasswall.CloudProxy.Api
{
    [ExcludeFromCodeCoverage]
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(o => o.AddPolicy(Constants.CORS_POLICY, builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            }));
            services.Configure<FormOptions>(x =>
            {
                x.MultipartBodyLengthLimit = long.MaxValue;
            });
            services.AddMvc()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.IgnoreNullValues = true;
            });
            services.ConfigureServices(Configuration);
            services.AddTransient<IFileUtility, FileUtility>();
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseCors(Constants.CORS_POLICY);
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.Use((context, next) =>
            {
                context.Response.Headers[Constants.Header.ACCESS_CONTROL_EXPOSE_HEADERS] = Constants.STAR;
                context.Response.Headers[Constants.Header.ACCESS_CONTROL_ALLOW_HEADERS] = Constants.STAR;
                context.Response.Headers[Constants.Header.ACCESS_CONTROL_ALLOW_ORIGIN] = Constants.STAR;
                context.Response.Headers[Constants.Header.VIA] = System.Environment.MachineName;
                return next.Invoke();
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
