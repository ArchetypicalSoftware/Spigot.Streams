using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.SDK.csharp.Events;
using KubeMQ.SDK.csharp.Subscription;
using KubeMqSender = KubeMQ.SDK.csharp.Events.LowLevel.Sender;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("Spigot.Tests")]

namespace Archetypical.Software.Spigot.Streams.KubeMQ
{
    public class KubeMqStream : ISpigotStream, IDisposable
    {
        private readonly ILogger<KubeMqStream> logger;
        private CancellationTokenSource source = new CancellationTokenSource();
        private KubeMqSender sender;
        private Subscriber subscriber;

        internal KubeMqStream(ILogger<KubeMqStream> logger)
        {
            this.logger = logger;

            CancellationToken token = source.Token;
        }

        public bool TrySend(byte[] data)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public async Task InitAsync(KubeMqSettings settings)
        {
            subscriber = new Subscriber(settings.ServerAddress, logger);

            SubscribeRequest subscribeRequest = CreateSubscribeRequest(settings);

            subscriber.SubscribeToEvents(subscribeRequest, HandleIncomingEvents, HandleIncomingError, source.Token);
            sender = new KubeMqSender(settings.ServerAddress, logger);
        }
    }

    public class KubeMqSettings
    {
        public SubscribeType SubscriptionType { get; set; } = SubscribeType.SubscribeTypeUndefined;
        public EventsStoreType EventsStoreType { get; set; } = EventsStoreType.Undefined;
        public int TypeValue { get; set; } = 0;
        public string ChannelName { get; set; } = "Spigot";
        public string Group { get; set; } = "";
        public string ServerAddress { get; set; }
    }
}