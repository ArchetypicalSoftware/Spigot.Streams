using System;
using Archetypical.Software.Spigot.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Archetypical.Software.Spigot.Streams.Redis
{
    public static class RedisBuilder
    {
        public static ISpigotBuilder AddRedis(this ISpigotBuilder src, Action<RedisSettings> builder)
        {
            var settings = new RedisSettings();
            builder(settings);
            src.Services.AddSingleton<ISpigotStream, RedisStream>(
                p =>
                {
                    var stream = new RedisStream(p.GetService<ILogger<RedisStream>>());
                    stream.InitAsync(settings).GetAwaiter().GetResult();
                    return stream;
                });
            return src;
        }
    }
}