using Glasswall.CloudProxy.Api.Utilities;
using Glasswall.CloudProxy.Common.Setup;
using Glasswall.CloudProxy.Common.Web.Models;
using Jaeger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Constants = Glasswall.CloudProxy.Common.Constants;

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
            services.AddTransient<IZipUtility, ZipUtility>();
            services.AddControllers();
            services.AddOpenTracing();

            // Adds the Jaeger Tracer.
            services.AddSingleton<ITracer>(serviceProvider =>
            {
                string serviceName = serviceProvider.GetRequiredService<IHostEnvironment>().ApplicationName;

                //string serviceName = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;

                ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

                Jaeger.Samplers.ISampler sampler = new Jaeger.Samplers.ConstSampler(sample: true);

                // This will log to a default localhost installation of Jaeger.
                Tracer tracer = new Tracer.Builder(serviceName)
                    .WithLoggerFactory(loggerFactory)
                    .WithSampler(new Jaeger.Samplers.ConstSampler(true))
                    .Build();

                //Environment.SetEnvironmentVariable("JAEGER_SERVICE_NAME", "rebuild-rest-api");
                //Environment.SetEnvironmentVariable("JAEGER_AGENT_HOST", "simplest-agent.observability.svc.cluster.local");
                //Environment.SetEnvironmentVariable("JAEGER_AGENT_PORT", "6831");
                //Environment.SetEnvironmentVariable("JAEGER_SAMPLER_TYPE", "const");

                //LoggerFactory loggerFactory = new LoggerFactory();

                //Configuration config = Jaeger.Configuration.FromEnv(loggerFactory);
                //ITracer tracer = config.GetTracer();

                if (!GlobalTracer.IsRegistered())
                {
                    // Allows code that can't use DI to also access the tracer.
                    GlobalTracer.Register(tracer);
                }

                return tracer;
            });

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
                ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                });
                ILogger logger = loggerFactory.CreateLogger<Startup>();
                UserAgentInfo userAgentInfo = new UserAgentInfo(context.Request.Headers[Constants.UserAgent.USER_AGENT]);
                logger.LogInformation($"UserAgent:: [{userAgentInfo?.ClientInfo?.String}]");

                context.Response.Headers[Constants.Header.ACCESS_CONTROL_EXPOSE_HEADERS] = Constants.STAR;
                context.Response.Headers[Constants.Header.ACCESS_CONTROL_ALLOW_HEADERS] = Constants.STAR;
                context.Response.Headers[Constants.Header.ACCESS_CONTROL_ALLOW_ORIGIN] = Constants.STAR;
                context.Response.Headers[Constants.Header.VIA] = Environment.MachineName;
                return next.Invoke();
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
