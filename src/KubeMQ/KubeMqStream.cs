using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.SDK.csharp.Events;
using KubeMQ.SDK.csharp.Subscription;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("Spigot.Tests")]

namespace Archetypical.Software.Spigot.Streams.KubeMQ
{
    public class KubeMqStream : ISpigotStream, IDisposable
    {
        private readonly ILogger<KubeMqStream> logger;
        private CancellationTokenSource source = new CancellationTokenSource();
        private Channel sender;
        private Subscriber subscriber;

        internal KubeMqStream(ILogger<KubeMqStream> logger)
        {
            this.logger = logger;
        }

        public bool TrySend(byte[] data)
        {
            var result = sender.SendEvent(new Event
            {
                Body = data,
            });
            if (!result.Sent)
            {
                logger.LogError(result.EventID, $"An error occurred attempting to send the event: {result.Error}");
            }
            return result.Sent;
        }

        private void HandleIncomingEvents(EventReceive @event)
        {
            if (@event != null)
            {
                DataArrived?.Invoke(this, @event.Body);
            }
        }

        protected SubscribeRequest CreateSubscribeRequest(KubeMqSettings settings)
        {
            Random random = new Random();

            SubscribeRequest subscribeRequest = new SubscribeRequest()

            {
                Channel = settings.ChannelName,

                ClientID = random.Next(9, 19999).ToString(),

                EventsStoreType = settings.EventsStoreType,

                EventsStoreTypeValue = (int)settings.EventsStoreType,
                Group = settings.Group,

                SubscribeType = settings.SubscriptionType
            };

            return subscribeRequest;
        }

        private void HandleIncomingError(Exception ex)

        {
            logger.LogWarning($"Received Exception :{ex}");
        }

        public event EventHandler<byte[]> DataArrived;

        public void Dispose()
        {
            sender.ClosesEventStreamAsync();
            source.Cancel();
            source.Dispose();
        }

        public async Task InitAsync(KubeMqSettings settings)
        {
            subscriber = new Subscriber(settings.ServerAddress, logger);

            var subscribeRequest = CreateSubscribeRequest(settings);
            await Task.Yield();
            subscriber.SubscribeToEvents(subscribeRequest, HandleIncomingEvents, HandleIncomingError, source.Token);
            sender = new Channel(new ChannelParameters
            {
                ChannelName = settings.ChannelName,
                ClientID = subscribeRequest.ClientID,
                KubeMQAddress = settings.ServerAddress,
                Store = true,
                Logger = logger
            });
        }
    }
}