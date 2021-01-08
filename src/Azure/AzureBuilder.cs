using System;
using Archetypical.Software.Spigot.Extensions;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Archetypical.Software.Spigot.Streams.Azure
{
    public static class AzureBuilder
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="src"></param>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static ISpigotBuilder AddAzureServiceBus(this ISpigotBuilder src, Action<AzureSettings> builder)
        {
            var settings = new AzureSettings();
            builder(settings);
            src.Services.AddSingleton<ITopicClient, TopicClient>(p =>
            {
                settings.ConnectionStringBuilder.EntityPath ??= settings.TopicName;
                var sb = new ServiceBusConnection(settings.ConnectionStringBuilder);
                return new TopicClient(sb, settings.TopicName, settings.RetryPolicy);
            });
            src.Services.AddSingleton<ISubscriptionClient, SubscriptionClient>(p =>
            {
                settings.ConnectionStringBuilder.EntityPath ??= settings.TopicName;
                var sb = new ServiceBusConnection(settings.ConnectionStringBuilder);
                return new SubscriptionClient(sb, settings.TopicName, settings.SubscriptionName
                    , settings.ReceiveMode, settings.RetryPolicy);
            });
            src.Services.AddSingleton<ISpigotStream, AzureServiceBusStream>();
            return src;
        }
    }
}