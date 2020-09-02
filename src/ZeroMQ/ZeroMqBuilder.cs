using System;
using Archetypical.Software.Spigot.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Archetypical.Software.Spigot.Streams.ZeroMQ
{
    public static class ZeroMqBuilder
    {
        public static ISpigotBuilder AddZeroMq(this ISpigotBuilder src, Action<ZeroMqSettings> builder)
        {
            var settings = new ZeroMqSettings();
            builder(settings);
            src.Services.AddSingleton<ISpigotStream, ZeroMqStream>(
                p =>
                {
                    var stream = new ZeroMqStream(p.GetService<ILogger<ZeroMqStream>>());
                    stream.Init(settings);
                    return stream;
                });
            return src;
        }
    }
}