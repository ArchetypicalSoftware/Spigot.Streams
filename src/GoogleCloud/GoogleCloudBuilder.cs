using System;
using Archetypical.Software.Spigot.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Archetypical.Software.Spigot.Streams.GoogleCloud
{
    public static class GoogleCloudBuilder
    {
        public static ISpigotBuilder AddGoogleCloudPubSub(this ISpigotBuilder src, Action<GoogleCloudSettings> builder)
        {
            var settings = new GoogleCloudSettings();
            builder(settings);
            src.Services.AddSingleton<ISpigotStream, GoogleCloudPubSubStream>(
                p =>
                {
                    var stream = new GoogleCloudPubSubStream(p.GetService<ILogger<GoogleCloudPubSubStream>>());
                    stream.InitAsync(settings).GetAwaiter().GetResult();
                    return stream;
                });
            return src;
        }
    }
}