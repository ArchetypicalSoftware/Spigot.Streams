using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Logging;

namespace Archetypical.Software.Spigot.Streams.Azure
{
    public class AzureSettings
    {
        public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Default;

        public ServiceBusConnectionStringBuilder ConnectionStringBuilder { get; set; } =
            new ServiceBusConnectionStringBuilder();

        public string TopicName { get; set; } = "spigot";
        public string SubscriptionName { get; set; } = "spigot";
        public ReceiveMode ReceiveMode { get; set; } = ReceiveMode.ReceiveAndDelete;

        public ILogger<AzureServiceBusStream> Logger { get; set; }
    }
}