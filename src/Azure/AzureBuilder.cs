using System;
using Archetypical.Software.Spigot.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Archetypical.Software.Spigot.Streams.Azure
{
    public static class AzureBuilder
    {
        public static ISpigotBuilder AddAzureServiceBus(this ISpigotBuilder src, Action<AzureSettings> builder)
        {
            var settings = new AzureSettings();
            builder(settings);
            src.Services.AddSingleton<ISpigotStream, AzureServiceBusStream>(
                p =>
                {
                    var stream = new AzureServiceBusStream(p.GetService<ILogger<AzureServiceBusStream>>());
                    stream.InitAsync(settings).GetAwaiter().GetResult();
                    return stream;
                });
            return src;
        }
    }
}