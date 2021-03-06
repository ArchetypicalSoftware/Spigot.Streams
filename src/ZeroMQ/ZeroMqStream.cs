﻿using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Spigot.Tests")]

namespace Archetypical.Software.Spigot.Streams.ZeroMQ
{
    /// <summary>
    /// To connect this stream, point to an XPUB, XSUB sockets. See https://netmq.readthedocs.io/en/latest/xpub-xsub/ for an example
    /// </summary>
    public class ZeroMqStream : ISpigotStream, IDisposable
    {
        private readonly ILogger<ZeroMqStream> logger;
        private ZeroMqSettings _settings;
        private PublisherSocket publisher;
        private SubscriberSocket subscriber;
        private Task SubscribingTask;
        private CancellationTokenSource tokenSource;

        internal ZeroMqStream(ILogger<ZeroMqStream> logger)
        {
            this.logger = logger;
        }

        ~ZeroMqStream()
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
            try
            {
                publisher.SendMoreFrame(_settings.TopicName).SendFrame(data);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ApplySocketOptions(SocketOptions socketOptions, SocketOptionSettings settingsOptions)
        {
            if (settingsOptions == null)
            {
                return;
            }

            socketOptions.Affinity = settingsOptions.Affinity;
            socketOptions.Backlog = settingsOptions.Backlog;
            socketOptions.DelayAttachOnConnect = settingsOptions.DelayAttachOnConnect;
            socketOptions.DisableTimeWait = settingsOptions.DisableTimeWait;
            socketOptions.Endian = settingsOptions.Endian;
            socketOptions.Identity = settingsOptions.Identity;
            socketOptions.IPv4Only = settingsOptions.IPv4Only;
            socketOptions.Linger = settingsOptions.Linger;
            socketOptions.MaxMsgSize = settingsOptions.MaxMsgSize;
            socketOptions.MulticastHops = settingsOptions.MulticastHops;
            socketOptions.MulticastRate = settingsOptions.MulticastRate;
            socketOptions.ReceiveBuffer = settingsOptions.ReceiveBuffer;
            socketOptions.ReceiveHighWatermark = settingsOptions.ReceiveHighWatermark;
            socketOptions.ReceiveLowWatermark = settingsOptions.ReceiveLowWatermark;
            socketOptions.ReconnectInterval = settingsOptions.ReconnectInterval;
            socketOptions.ReconnectIntervalMax = settingsOptions.ReconnectIntervalMax;
            socketOptions.SendBuffer = settingsOptions.SendBuffer;
            socketOptions.SendHighWatermark = settingsOptions.SendHighWatermark;
            socketOptions.SendLowWatermark = settingsOptions.SendLowWatermark;
            socketOptions.TcpKeepalive = settingsOptions.TcpKeepalive;
            socketOptions.TcpKeepaliveIdle = settingsOptions.TcpKeepaliveIdle;
            socketOptions.TcpKeepaliveInterval = settingsOptions.TcpKeepaliveInterval;
        }

        internal void Init(ZeroMqSettings settings)
        {
            tokenSource = new CancellationTokenSource();
            _settings = settings;
            publisher = new PublisherSocket();
            publisher.Connect(settings.XSubscriberSocketConnectionString);
            subscriber = new SubscriberSocket();
            subscriber.Connect(settings.XPublisherSocketConnectionString);
            ApplySocketOptions(publisher.Options, settings.PublishingSocketOptions);
            ApplySocketOptions(subscriber.Options, settings.SubscribingSocketOptions);
            subscriber.Subscribe(settings.TopicName);
            SubscribingTask = Task.Factory.StartNew(() => Subscriber_Receive(tokenSource.Token));
        }

        private void ReleaseUnmanagedResources()
        {
            tokenSource.Cancel(false);
            publisher.Close();
            subscriber.Close();
            publisher.Dispose();
            subscriber.Dispose();
        }

        private void Subscriber_Receive(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool more; byte[] bytes;
                do
                {
                    bytes = subscriber.ReceiveFrameBytes(out more);
                } while (more);
                DataArrived?.Invoke(this, bytes);
            }
        }
    }
}