using System.Collections.Generic;
using RabbitMQ.Client;

namespace Archetypical.Software.Spigot.Streams.RabbitMq
{
    public class RabbitMqSettings
    {
        public ConnectionFactory ConnectionFactory { get; set; }

        public QueueSettings Queue { get; set; } = new QueueSettings();
        public QosSettings Qos { get; set; } = new QosSettings();

        public class QueueSettings
        {
            public string Name { get; set; } = "archetypical_software_spigot";

            public bool Durable { get; set; } = true;
            public bool Exclusive { get; set; }
            public bool AutoDelete { get; set; }
            public IDictionary<string, object> Arguments { get; set; }
        }

        public class QosSettings
        {
            public bool Global { get; set; }
            public ushort PrefetchCount { get; set; } = 1;
            public uint PrefetchSize { get; set; }
        }
    }
}