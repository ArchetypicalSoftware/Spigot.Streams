using KubeMQ.SDK.csharp.Subscription;

namespace Archetypical.Software.Spigot.Streams.KubeMQ
{
    public class KubeMqSettings
    {
        public SubscribeType SubscriptionType { get; set; } = SubscribeType.EventsStore;
        public EventsStoreType EventsStoreType { get; set; } = EventsStoreType.StartFromFirst;
        public string ChannelName { get; set; } = "Spigot";
        public string Group { get; set; } = "";
        public string ServerAddress { get; set; }
    }
}