using StackExchange.Redis;

namespace Archetypical.Software.Spigot.Streams.Redis
{
    public class RedisSettings
    {
        public CommandFlags CommandFlags { get; set; } = CommandFlags.None;
        public ConfigurationOptions ConfigurationOptions { get; set; }
        public string TopicName { get; set; }
    }
}