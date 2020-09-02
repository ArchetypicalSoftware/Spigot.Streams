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
            src.Services.AddSingleton<ISpigotStream, SnsStream>(
                p =>
                {
                    var stream = new SnsStream(p.GetService<ILogger<SnsStream>>());
                    stream.InitAsync(settings).GetAwaiter().GetResult();
                    return stream;
                });
            return src;
        }

        public static ISpigotBuilder AddAwsKinesis(this ISpigotBuilder src, Action<KinesisSettings> builder)
        {
            var settings = new KinesisSettings();
            builder(settings);
            src.Services.AddSingleton<ISpigotStream, KinesisStream>(
                p =>
                {
                    var stream = new KinesisStream(p.GetService<ILogger<KinesisStream>>());
                    stream.InitAsync(settings).GetAwaiter().GetResult();
                    return stream;
                });
            return src;
        }
    }
}