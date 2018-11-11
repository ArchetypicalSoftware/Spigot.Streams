using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace Archetypical.Software.Spigot.Streams.Redis
{
    public class Stream : ISpigotStream, IDisposable
    {
        private RedisSettings _settings;

        private ConnectionMultiplexer _redis;

        private ISubscriber _subscriber;

        ~Stream()
        {
            ReleaseUnmanagedResources();
        }

        public event EventHandler<byte[]> DataArrived;

        public static async Task<Stream> BuildAsync(Action<RedisSettings> builder)
        {
            var settings = new RedisSettings();
            builder(settings);
            var instance = new Stream();
            await instance.Init(settings);
            return instance;
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        public bool TrySend(byte[] data)
        {
            return _subscriber.Publish(_settings.TopicName, data, _settings.CommandFlags) > 0;
        }

        private async Task Init(RedisSettings settings)
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