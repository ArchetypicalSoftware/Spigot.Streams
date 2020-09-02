using System;
using Microsoft.Extensions.Logging;
using Archetypical.Software.Spigot.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Archetypical.Software.Spigot.Streams.AWS
{
    public static class AwsBuilder
    {
        public static ISpigotBuilder AddAwsSns(this ISpigotBuilder src, Action<SnsStreamSettings> builder)
        {
            var settings = new SnsStreamSettings();
            builder(settings);
            src.Services.AddSingleton<ISpigotStream>(
                p =>
                {
                    var stream = p.GetService<SnsStream>();
                    stream.InitAsync(settings).GetAwaiter().GetResult();
                    return stream;
                });
            return src;
        }

        public static ISpigotBuilder AddAwsKinesis(this ISpigotBuilder src, Action<KinesisSettings> builder)
        {
            var settings = new KinesisSettings();
            builder(settings);
            src.Services.AddSingleton<ISpigotStream>(
                p =>
                {
                    var stream = p.GetService<KinesisStream>();
                    stream.InitAsync(settings).GetAwaiter().GetResult();
                    return stream;
                });
            return src;
        }
    }
}