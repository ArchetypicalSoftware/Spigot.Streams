using System;
using Archetypical.Software.Spigot.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Archetypical.Software.Spigot.Streams.RabbitMq
{
    public static class RabbitMqBuilder
    {
        public static ISpigotBuilder AddRabbitMq(this ISpigotBuilder src, Action<RabbitMqSettings> builder)
        {
            var settings = new RabbitMqSettings();
            builder(settings);
            src.Services.AddSingleton<ISpigotStream>(
                p =>
                {
                    var stream = p.GetService<RabbitMqStream>();
                    stream.InitAsync(settings).GetAwaiter().GetResult();
                    return stream;
                });
            return src;
        }
    }
}