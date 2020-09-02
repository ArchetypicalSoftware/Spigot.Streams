using StackExchange.Redis;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("Spigot.Tests")]

namespace Archetypical.Software.Spigot.Streams.Redis
{
    public class RedisStream : ISpigotStream, IDisposable
    {
        private readonly ILogger<RedisStream> logger;
        private ConnectionMultiplexer _redis;
        private RedisSettings _settings;
        private ISubscriber _subscriber;

        internal RedisStream(ILogger<RedisStream> logger)
        {
            this.logger = logger;
        }

        ~RedisStream()
        {
            ReleaseUnmanagedResources();
        }

        public event EventHandler<byte[]> DataArrived;

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        public bool TrySend(byte[] data)
        {
            return _subscriber.Publish(_settings.TopicName, data, _settings.CommandFlags) > 0;
        }

        internal async Task InitAsync(RedisSettings settings)
        {
            _settings = settings;
            _redis = await ConnectionMultiplexer.ConnectAsync(settings.ConfigurationOptions);
            _subscriber = _redis.GetSubscriber();

            await _subscriber.SubscribeAsync(settings.TopicName, (channel, value) =>
            {
                if (DataArrived != null)
                {
                    byte[] bytes = (RedisValue)value;
                    if (bytes != null)
                        DataArrived.Invoke(this, bytes);
                }
            }, settings.CommandFlags);
        }

        private void ReleaseUnmanagedResources()
        {
            _subscriber.UnsubscribeAll();
            _redis.Dispose();
        }
    }
}