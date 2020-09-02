using System;
using Archetypical.Software.Spigot.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Archetypical.Software.Spigot.Streams.KubeMQ
{
    public static class KubeMqBuilder
    {
        public static ISpigotBuilder AddKubeMq(this ISpigotBuilder src, Action<KubeMqSettings> builder)
        {
            var settings = new KubeMqSettings();
            builder(settings);
            src.Services.AddSingleton<ISpigotStream, KubeMqStream>(
                p =>
                {
                    var stream = new KubeMqStream(p.GetService<ILogger<KubeMqStream>>());
                    stream.InitAsync(settings).GetAwaiter().GetResult();
                    return stream;
                });
            return src;
        }
    }
}