using System;
using NetMQ;

namespace Archetypical.Software.Spigot.Streams.ZeroMQ
{
    public class ZeroMqSettings
    {
        public ZeroMqSettings()
        {
        }

        public string XSubscriberSocketConnectionString { get; set; }
        public string XPublisherSocketConnectionString { get; set; }
        public SocketOptionSettings PublishingSocketOptions { get; set; }
        public SocketOptionSettings SubscribingSocketOptions { get; set; }
        public string TopicName { get; set; } = "archetypical_software_spigot";
    }

    public class SocketOptionSettings
    {
        public long Affinity { get; set; }
        public byte[] Identity { get; set; }
        public int MulticastRate { get; set; }
        public TimeSpan MulticastRecoveryInterval { get; set; }
        public int SendBuffer { get; set; }
        public int ReceiveBuffer { get; set; }
        public bool ReceiveMore { get; }
        public TimeSpan Linger { get; set; }
        public TimeSpan ReconnectInterval { get; set; }
        public TimeSpan ReconnectIntervalMax { get; set; }
        public int Backlog { get; set; }
        public long MaxMsgSize { get; set; }
        public int SendHighWatermark { get; set; }
        public int ReceiveHighWatermark { get; set; }
        public int SendLowWatermark { get; set; }
        public int ReceiveLowWatermark { get; set; }
        public int MulticastHops { get; set; }
        public bool IPv4Only { get; set; }
        public bool TcpKeepalive { get; set; }
        public TimeSpan TcpKeepaliveIdle { get; set; }
        public TimeSpan TcpKeepaliveInterval { get; set; }
        public bool DelayAttachOnConnect { get; set; }

        public Endianness Endian { get; set; }

        public bool DisableTimeWait { get; set; }
        public int PgmMaxTransportServiceDataUnitLength { get; set; }
    }
}