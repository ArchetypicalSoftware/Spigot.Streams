using System.Collections.Generic;

namespace Archetypical.Software.Spigot.Streams.Kafka
{
    public class KafkaSettings
    {
        public IDictionary<string, object> Consumeronfig { get; set; }
        public bool ManualPoll { get; set; }
        public bool DisableDeliveryReports { get; set; } = true;
        public IDictionary<string, object> ProducerConfig { get; set; }
        public string TopicName { get; set; }
    }
}