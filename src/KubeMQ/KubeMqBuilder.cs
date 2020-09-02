using System;
using Archetypical.Software.Spigot.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Archetypical.Software.Spigot.Streams.KubeMQ
{
    public static class KubeMqBuilder
    {
        public static ISpigotBuilder AddKubeMq(this ISpigotBuilder src, Action<KubeMqSettings> builder)
        {
            var settings = new KubeMqSettings();
            builder(settings);
            src.Services.AddSingleton<ISpigotStream>(
                p =>
                {
                    var stream = p.GetService<KubeMqStream>();
                    stream.InitAsync(settings).GetAwaiter().GetResult();
                    return stream;
                });
            return src;
        }
    }
}