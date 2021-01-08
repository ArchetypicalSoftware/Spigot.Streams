using Google.Cloud.PubSub.V1;
using Microsoft.Extensions.Logging;
using System;
using Google.Api.Gax.Grpc;
using Grpc.Core;

namespace Archetypical.Software.Spigot.Streams.GoogleCloud
{
    public class GoogleCloudSettings
    {
        public string SubscriptionName { get; set; } = $"archetypical_software_spigot_{Guid.NewGuid()}";

        public SubscriberClient.ClientCreationSettings ClientCreationSettings { get; set; } =
            new SubscriberClient.ClientCreationSettings();

        public SubscriberClient.Settings SubscriberClientSettings { get; set; } = new SubscriberClient.Settings();

        /// <summary>
        ///Your Google Cloud Platform project ID
        /// </summary>
        public string ProjectId { get; set; }

        public string TopicId { get; set; } = "archetypical_software_spigot";

        public PublisherServiceApiSettings PublisherServiceApiSettings { get; set; } = null;

        public SubscriberServiceApiSettings SubscriberServiceApiSettings { get; set; } = null;
        public string Endpoint { get; set; }
        public ChannelCredentials ChannelCredentials { get; set; } = ChannelCredentials.Insecure;
    }
}