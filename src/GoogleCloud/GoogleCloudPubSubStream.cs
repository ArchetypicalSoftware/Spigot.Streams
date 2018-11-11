using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;

namespace Archetypical.Software.Spigot.Streams.GoogleCloud
{
    public class GoogleCloudPubSubStream : ISpigotStream, IDisposable
    {
        private CancellationTokenSource _cancellationTokenSource;

        public bool TrySend(byte[] data)
        {
            var response = publisher.Publish(topicName, new List<PubsubMessage>() { new PubsubMessage { Data = ByteString.CopyFrom(data) } });
            return response.MessageIds.Count == 1;
        }

        public static async Task<GoogleCloudPubSubStream> BuildAsync(Action<GoogleCloudSettings> builder)
        {
            var settings = new GoogleCloudSettings();
            builder(settings);
            var instance = new GoogleCloudPubSubStream();
            await instance.Init(settings);
            return instance;
        }

        private PublisherServiceApiClient publisher; private TopicName topicName; private SubscriberClient subscriber;

        private async Task Init(GoogleCloudSettings settings)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            // Instantiates a client

            publisher = PublisherServiceApiClient.Create();

            // The name for the new topic

            topicName = new TopicName(settings.ProjectId, settings.TopicId);

            // Creates the new topic
            try
            {
                Topic topic = publisher.CreateTopic(topicName);
                Console.WriteLine($"Topic {topic.Name} created.");
            }
            catch (Grpc.Core.RpcException e)
                when (e.Status.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
            {
                Console.WriteLine($"Topic {topicName} already exists.");
            }

            var subscriptionName = new SubscriptionName(settings.ProjectId, settings.SubscriptionName);
            var api = await SubscriberServiceApiClient.CreateAsync();

            try
            {
                var subscription = await api.CreateSubscriptionAsync(subscriptionName, topicName, new PushConfig(), 10);
            }
            catch (Exception ee)
            {
            }

            subscriber = await SubscriberClient.CreateAsync(subscriptionName, settings.ClientCreationSettings,
                settings.SubscriberClientSettings);
            subscriber.StartAsync(HandleMessageAsync);
        }

        private async Task<SubscriberClient.Reply> HandleMessageAsync(PubsubMessage msg, CancellationToken cancellationToken)
        {
            if (DataArrived != null)
            {
                try
                {
                    var bytes = new byte[msg.Data.Length];
                    msg.Data.CopyTo(bytes, 0);
                    DataArrived.Invoke(this, bytes);
                }
                catch (Exception ex)
                {
                    return SubscriberClient.Reply.Nack;
                }
            }
            return SubscriberClient.Reply.Ack;
        }

        private GoogleCloudPubSubStream()
        {
        }

        public event EventHandler<byte[]> DataArrived;

        private void ReleaseUnmanagedResources()
        {
            subscriber?.StopAsync(_cancellationTokenSource.Token).GetAwaiter().GetResult();
            _cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~GoogleCloudPubSubStream()
        {
            ReleaseUnmanagedResources();
        }
    }
}