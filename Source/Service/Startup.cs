using Glasswall.CloudProxy.Common.Configuration;
using Glasswall.CloudProxy.Common.Setup;
using Glasswall.CloudProxy.Common.Utilities;
using Glasswall.CloudProxy.Common.Web.Models;
using Jaeger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using OpenTracing;
using OpenTracing.Util;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
            services.AddSwaggerGen(c =>
            {
                c.DocumentFilter<CloudSDKDocumentFilter>();
            });

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
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(env.ContentRootPath, Constants.STATIC_FILES_FOLDER_Name)),
            });
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.yaml", "Cloud SDK Api");
                c.InjectJavascript("/Swg/func.js");
                c.InjectJavascript("/Swg/toast/toastify.js");
                c.InjectStylesheet("/Swg/toast/toastify.css");
            });
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
                IVersionConfiguration versionConfig = app.ApplicationServices.GetService<IVersionConfiguration>();
                context.Response.Headers[Constants.Header.ACCESS_CONTROL_EXPOSE_HEADERS] = Constants.STAR;
                context.Response.Headers[Constants.Header.ACCESS_CONTROL_ALLOW_HEADERS] = Constants.STAR;
                context.Response.Headers[Constants.Header.ACCESS_CONTROL_ALLOW_ORIGIN] = Constants.STAR;
                context.Response.Headers[Constants.Header.VIA] = Environment.MachineName;
                context.Response.Headers[Constants.Header.SDK_ENGINE_VERSION] = versionConfig.SDKEngineVersion;
                context.Response.Headers[Constants.Header.SDK_API_VERSION] = versionConfig.SDKApiVersion;
                return next.Invoke();
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }

    public class CloudSDKDocumentFilter : IDocumentFilter
    {
        private readonly IHostEnvironment _hostEnvironment;
        public CloudSDKDocumentFilter(IHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            string swaggerFilePath = Path.Combine(_hostEnvironment.ContentRootPath, Constants.STATIC_FILES_FOLDER_Name, Constants.SWAGGER_FOLDER_Name, Constants.SWAGGER_FILENAME);
            using FileStream fileStream = File.Open(swaggerFilePath, FileMode.Open, FileAccess.Read);
            OpenApiDocument openApiDocument = new OpenApiStreamReader().Read(fileStream, out OpenApiDiagnostic diagnostic);
            swaggerDoc.Components = openApiDocument.Components;
            swaggerDoc.Extensions = openApiDocument.Extensions;
            swaggerDoc.ExternalDocs = openApiDocument.ExternalDocs;
            swaggerDoc.Info = openApiDocument.Info;
            swaggerDoc.Paths = openApiDocument.Paths;
            swaggerDoc.SecurityRequirements = openApiDocument.SecurityRequirements;
            swaggerDoc.Tags = openApiDocument.Tags;
        }
    }
}
