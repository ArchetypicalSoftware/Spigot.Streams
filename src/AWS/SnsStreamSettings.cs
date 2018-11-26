using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Microsoft.Extensions.Logging;

namespace Archetypical.Software.Spigot.Streams.AWS
{
    public class SnsStreamSettings
    {
        public ILogger<SnsStream> Logger { get; set; }

        public CallbackSettings CallbackSettings { get; set; } = new CallbackSettings();

        public AmazonSimpleNotificationServiceConfig AmazonSimpleNotificationServiceConfig { get; set; } =
            new AmazonSimpleNotificationServiceConfig();

        public AWSCredentials AwsCredentials { get; set; }

        public string TopicName { get; set; } = "Spigot.Stream";
    }
}