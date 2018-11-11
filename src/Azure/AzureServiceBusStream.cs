using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;

namespace Archetypical.Software.Spigot.Streams.Azure
{
    public class AzureServiceBusStream : ISpigotStream, IDisposable
    {
        private TopicClient client;
        private ISubscriptionClient subscriptionClient;
        private CancellationTokenSource _cancellationTokenSource;

        public bool TrySend(byte[] data)
        {
            try
            {
                client.SendAsync(new Message(data)).GetAwaiter().GetResult();
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public static async Task<AzureServiceBusStream> BuildAsync(Action<AzureSettings> builder)
        {
            var settings = new AzureSettings();
            builder(settings);
            var instance = new AzureServiceBusStream();
            await instance.Init(settings);
            return instance;
        }

        private void RegisterOnMessageHandlerAndReceiveMessages()
        {
            // Configure the message handler options in terms of exception handling, number of concurrent messages to deliver, etc.
            var messageHandlerOptions = new MessageHandlerOptions(ExceptionReceivedHandler)
            {
                // Maximum number of concurrent calls to the callback ProcessMessagesAsync(), set to 1 for simplicity.
                // Set it according to how many messages the application wants to process in parallel.
                MaxConcurrentCalls = 5,

                // Indicates whether the message pump should automatically complete the messages after returning from user callback.
                // False below indicates the complete operation is handled by the user callback as in ProcessMessagesAsync().
                AutoComplete = false
            };

            // Register the function that processes messages.
            subscriptionClient.RegisterMessageHandler((msg, token) =>
            {
                if (DataArrived != null)
                    return Task.Factory.FromAsync(DataArrived.BeginInvoke(this, msg.Body, DataArrived.EndInvoke, null),
                        DataArrived.EndInvoke);
                return Task.FromResult(0);
            }, messageHandlerOptions);
        }

        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs arg)
        {
            throw new NotImplementedException();
        }

        private async Task Init(AzureSettings settings)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            settings.ConnectionStringBuilder.EntityPath =
                settings.ConnectionStringBuilder.EntityPath ?? settings.TopicName;
            var sb = new ServiceBusConnection(settings.ConnectionStringBuilder);
            client = new TopicClient(sb, settings.TopicName, settings.RetryPolicy);
            subscriptionClient = new SubscriptionClient(sb, settings.TopicName, settings.SubscriptionName
                , settings.ReceiveMode, settings.RetryPolicy);

            RegisterOnMessageHandlerAndReceiveMessages();
        }

        private AzureServiceBusStream()
        {
        }

        public event EventHandler<byte[]> DataArrived;

        private void ReleaseUnmanagedResources()
        {
            subscriptionClient?.CloseAsync().GetAwaiter().GetResult();
            client?.CloseAsync().GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~AzureServiceBusStream()
        {
            ReleaseUnmanagedResources();
        }
    }

    public class AzureSettings
    {
        public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Default;
        public ServiceBusConnectionStringBuilder ConnectionStringBuilder { get; set; }
        public string TopicName { get; set; }
        public string SubscriptionName { get; set; }
        public ReceiveMode ReceiveMode { get; set; } = ReceiveMode.PeekLock;
    }
}