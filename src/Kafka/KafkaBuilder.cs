using System;
using Archetypical.Software.Spigot.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Archetypical.Software.Spigot.Streams.Kafka
{
    public static class KafkaBuilder
    {
        public static ISpigotBuilder AddKafka(this ISpigotBuilder src, Action<KafkaSettings> builder)
        {
            var settings = new KafkaSettings();
            builder(settings);
            src.Services.AddSingleton<ISpigotStream, KafkaStream>(
                p =>
                {
                    var stream = new KafkaStream(p.GetService<ILogger<KafkaStream>>());
                    stream.InitAsync(settings).GetAwaiter().GetResult();
                    return stream;
                });
            return src;
        }
    }
}