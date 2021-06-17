using Glasswall.CloudProxy.Common.AdaptationService;
using Glasswall.CloudProxy.Common.ConfigLoaders;
using Glasswall.CloudProxy.Common.Configuration;
using Glasswall.CloudProxy.Common.HttpService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Glasswall.CloudProxy.Common.Setup
{
    public static class ServiceCollectionExtensionMethods
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection serviceCollection, IConfiguration configuration)
        {
            IQueueConfiguration queueConfig = RabbitMqDefaultConfigLoader.SetDefaults(new RabbitMqQueueConfiguration());
            configuration.Bind(queueConfig);
            serviceCollection.AddTransient<IQueueConfiguration>(x => queueConfig);

            IStoreConfiguration storeConfig = AdaptationStoreConfigLoader.SetDefaults(new AdaptationStoreConfiguration());
            configuration.Bind(storeConfig);
            serviceCollection.AddTransient<IStoreConfiguration>(x => storeConfig);

            IProcessingConfiguration processingConfig = IcapProcessingConfigLoader.SetDefaults(new IcapProcessingConfiguration());
            configuration.Bind(processingConfig);
            serviceCollection.AddTransient<IProcessingConfiguration>(x => processingConfig);

            ICloudSdkConfiguration versionConfiguration = CloudSdkConfigLoader.SetDefaults(new CloudSdkConfiguration());
            configuration.Bind(versionConfiguration);
            serviceCollection.AddTransient<ICloudSdkConfiguration>(x => versionConfiguration);

            serviceCollection.AddTransient(typeof(IAdaptationServiceClient<>), typeof(RabbitMqClient<>));
            serviceCollection.AddTransient<IResponseProcessor, AdaptationOutcomeProcessor>();
            serviceCollection.AddTransient<IHttpService, HttpService.HttpService>();
            serviceCollection.AddSingleton<IRabbitMqConnectionFactory, RabbitMqConnectionFactory>();

            return serviceCollection;
        }
    }
}
