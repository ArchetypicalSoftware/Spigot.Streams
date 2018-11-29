using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System;

namespace Archetypical.Software.Spigot.Streams.Kafka
{
    /// <summary>
    /// See https://github.com/confluentinc/confluent-kafka-dotnet for connection settings
    /// </summary>
    public class KafkaSettings
    {
        public ILogger<KafkaStream> Logger { get; set; }

        /// <summary>
        /// At a minimum, 'bootstrap.servers' and 'group.id' must be specified.
        /// </summary>
        public ConsumerConfig ConsumerConfig { get; set; } = new ConsumerConfig();

        /// <summary>
        /// At a minimum, 'bootstrap.servers'
        /// </summary>
        public ProducerConfig ProducerConfig { get; set; } = new ProducerConfig();

        public TopicMetadata Topic { get; set; } = new TopicMetadata();

        public class TopicMetadata
        {
            public string Name { get; set; } = "Spigot";

            public int NumberOfPartitions { get; set; } = 3;

            public short Replicas { get; set; } = 3;

            public int RetentionBytes { get; set; } = 1024;

            public double RetentionMs { get; set; } = TimeSpan.FromMinutes(5).TotalMilliseconds;
        }
    }
}