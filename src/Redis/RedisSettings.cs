using StackExchange.Redis;

namespace Archetypical.Software.Spigot.Streams.Redis
{
    public class RedisSettings
    {
        public CommandFlags CommandFlags { get; set; } = CommandFlags.None;
        public ConfigurationOptions ConfigurationOptions { get; set; } = new ConfigurationOptions();

        public string TopicName { get; set; } = "Spigot";
    }
}