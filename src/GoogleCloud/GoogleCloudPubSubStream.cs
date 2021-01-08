using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

[assembly: InternalsVisibleTo("Spigot.Tests")]

namespace Archetypical.Software.Spigot.Streams.GoogleCloud
{
    public class GoogleCloudPubSubStream : ISpigotStream, IDisposable
    {
        private CancellationTokenSource _cancellationTokenSource;

        private ILogger<GoogleCloudPubSubStream> Logger;
        private PublisherServiceApiClient publisher;
        private Task subscriberTask;
        private SubscriberClient subscriber;

        private TopicName topicName;

        internal GoogleCloudPubSubStream(ILogger<GoogleCloudPubSubStream> logger)
        {
            Logger = logger;
        }

        ~GoogleCloudPubSubStream()
        {
            ReleaseUnmanagedResources();
        }

        public event EventHandler<byte[]> DataArrived;

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        public bool TrySend(byte[] data)
        {
            Logger?.LogDebug("Attempting to send {0} bytes", data.Length);
            var response = publisher.Publish(topicName, new List<PubsubMessage>() { new PubsubMessage { Data = ByteString.CopyFrom(data) } });
            Logger?.LogDebug("Successfully sent {0} messages with Ids {1}", response.MessageIds.Count, string.Join(",", response.MessageIds));
            return response.MessageIds.Count == 1;
        }

        private async Task<SubscriberClient.Reply> HandleMessageAsync(PubsubMessage msg, CancellationToken cancellationToken)
        {
            Logger?.LogTrace("Data arrived messageID:{0}", msg.MessageId);
            if (DataArrived == null)
            {
                Logger?.LogTrace("No handler attached. Auto-acking message");
                return SubscriberClient.Reply.Ack;
            }

            try
            {
                var bytes = new byte[msg.Data.Length];
                msg.Data.CopyTo(bytes, 0);
                DataArrived.Invoke(this, bytes);
            }
            catch (Exception ex)
            {
                Logger?.LogDebug("Exception: {0}", ex.Message);
                return SubscriberClient.Reply.Nack;
            }
            return SubscriberClient.Reply.Ack;
        }

        internal async Task InitAsync(GoogleCloudSettings settings)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var subscriptionName = new SubscriptionName(settings.ProjectId, settings.SubscriptionName);
            SubscriberServiceApiClient api;

            // Instantiates a client
            if (settings.Endpoint == null || settings.ChannelCredentials == null)
            {
                publisher = await PublisherServiceApiClient.CreateAsync();
                api = await SubscriberServiceApiClient.CreateAsync();
            }
            else
            {
                publisher = await new PublisherServiceApiClientBuilder()
                {
                    Settings = settings.PublisherServiceApiSettings,
                    Endpoint = settings.Endpoint,
                    ChannelCredentials = settings.ChannelCredentials
                }.BuildAsync();
                api = await new SubscriberServiceApiClientBuilder()
                {
                    Settings = settings.SubscriberServiceApiSettings,
                    Endpoint = settings.Endpoint,
                    ChannelCredentials = settings.ChannelCredentials
                }.BuildAsync();
            }
            // The name for the new topic

            topicName = new TopicName(settings.ProjectId, settings.TopicId);

            // Creates the new topic
            try
            {
                var topic = publisher.CreateTopic(topicName);
                Logger?.LogInformation($"Topic {topic.Name} created.");
            }
            catch (RpcException e)
                when (e.Status.StatusCode == StatusCode.AlreadyExists)
            {
                Logger?.LogInformation($"Topic {topicName} already exists.");
            }
            catch (Exception) { }

            try
            {
                var subscription = await api.CreateSubscriptionAsync(subscriptionName, topicName, new PushConfig(), 10);
            }
            catch (Exception ee)
            {
                Logger?.LogInformation(ee.Message);
            }

            subscriber = await SubscriberClient.CreateAsync(subscriptionName, settings.ClientCreationSettings,
                settings.SubscriberClientSettings);
            subscriberTask = subscriber.StartAsync(HandleMessageAsync).ContinueWith(t =>
                {
                    Logger?.LogError(t.Exception, "An error attempting to handle messages occurred. {0}",
                        t.Exception.Message);
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private void ReleaseUnmanagedResources()
        {
            subscriber?.StopAsync(_cancellationTokenSource.Token).GetAwaiter().GetResult();
            _cancellationTokenSource?.Cancel();
        }
    }
}