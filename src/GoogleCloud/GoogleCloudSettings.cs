using Google.Cloud.PubSub.V1;

namespace Archetypical.Software.Spigot.Streams.GoogleCloud
{
    public class GoogleCloudSettings
    {
        public string SubscriptionName { get; set; } = "archetypical_software_spigot";
        public SubscriberClient.ClientCreationSettings ClientCreationSettings { get; set; }
        public SubscriberClient.Settings SubscriberClientSettings { get; set; }

        /// <summary>
        ///Your Google Cloud Platform project ID
        /// </summary>
        public string ProjectId { get; set; }

        public string TopicId { get; set; } = "archetypical_software_spigot";
    }
}