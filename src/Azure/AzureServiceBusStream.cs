using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Archetypical.Software.Spigot.Streams.Azure
{
    public class AzureServiceBusStream : ISpigotStream, IDisposable
    {
        private TopicClient client;
        private ISubscriptionClient subscriptionClient;
        private ILogger<AzureServiceBusStream> Logger;

        public bool TrySend(byte[] data)
        {
            try
            {
                Logger?.LogDebug("Attempting to send {0} bytes", data.Length);
                var msg = new Message(data);
                client.SendAsync(msg).GetAwaiter().GetResult();
                return true;
            }
            catch (Exception e)
            {
                Logger?.LogDebug("Error : {0}", e.Message);
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
                AutoComplete = true
            };

            // Register the function that processes messages.
            subscriptionClient.RegisterMessageHandler((msg, token) =>
            {
                Logger?.LogDebug("Data received with message Id {0}", msg.MessageId);
                if (DataArrived == null)
                {
                    Logger?.LogDebug("Handler is null, skipping Invocation");
                }
                try
                {
                    DataArrived?.Invoke(this, msg.Body);
                }
                catch (Exception)
                {
                }
                return Task.CompletedTask;
            }, messageHandlerOptions);
        }

        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs arg)
        {
            throw new NotImplementedException();
        }

        private async Task Init(AzureSettings settings)
        {
            Logger = settings.Logger;
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
}