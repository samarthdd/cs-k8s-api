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
            serviceCollection.AddSingleton<IQueueConfiguration>(queueConfig);

            IStoreConfiguration storeConfig = AdaptationStoreConfigLoader.SetDefaults(new AdaptationStoreConfiguration());
            configuration.Bind(storeConfig);
            serviceCollection.AddSingleton<IStoreConfiguration>(storeConfig);

            IProcessingConfiguration processingConfig = IcapProcessingConfigLoader.SetDefaults(new IcapProcessingConfiguration());
            configuration.Bind(processingConfig);
            serviceCollection.AddSingleton<IProcessingConfiguration>(processingConfig);

            ICloudSdkConfiguration versionConfiguration = CloudSdkConfigLoader.SetDefaults(new CloudSdkConfiguration());
            configuration.Bind(versionConfiguration);
            serviceCollection.AddSingleton<ICloudSdkConfiguration>(versionConfiguration);

            serviceCollection.AddTransient(typeof(IAdaptationServiceClient<>), typeof(RabbitMqClient<>));
            serviceCollection.AddTransient<IResponseProcessor, AdaptationOutcomeProcessor>();
            serviceCollection.AddSingleton<IHttpService, HttpService.HttpService>();

            return serviceCollection;
        }
    }
}
