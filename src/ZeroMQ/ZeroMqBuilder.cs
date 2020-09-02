using System;
using Archetypical.Software.Spigot.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Archetypical.Software.Spigot.Streams.ZeroMQ
{
    public static class ZeroMqBuilder
    {
        public static ISpigotBuilder AddZeroMq(this ISpigotBuilder src, Action<ZeroMqSettings> builder)
        {
            var settings = new ZeroMqSettings();
            builder(settings);
            src.Services.AddSingleton<ISpigotStream>(
                p =>
                {
                    var stream = p.GetService<ZeroMqStream>();
                    stream.Init(settings);
                    return stream;
                });
            return src;
        }
    }
}