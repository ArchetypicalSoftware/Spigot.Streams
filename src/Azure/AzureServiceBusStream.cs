using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Spigot.Tests")]

namespace Archetypical.Software.Spigot.Streams.Azure
{
    /// <summary>
    /// Connects Spigot with Azure Service Bus
    /// </summary>
    public class AzureServiceBusStream : ISpigotStream, IDisposable
    {
        private readonly ITopicClient _client;
        private readonly ILogger<AzureServiceBusStream> _logger;
        private readonly ISubscriptionClient _subscriptionClient;

        public AzureServiceBusStream(
            ILogger<AzureServiceBusStream> logger,
            ITopicClient topicClient,
            ISubscriptionClient subscriptionClient)
        {
            _logger = logger;
            _client = topicClient;
            _subscriptionClient = subscriptionClient;
            RegisterOnMessageHandlerAndReceiveMessages();
        }

        ~AzureServiceBusStream()
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
                _logger?.LogDebug("Attempting to send {0} bytes", data.Length);
                var msg = new Message(data);
                _client.SendAsync(msg).GetAwaiter().GetResult();
                return true;
            }
            catch (Exception e)
            {
                _logger?.LogDebug("Error : {0}", e.Message);
                return false;
            }
        }

        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs arg)
        {
            throw new NotImplementedException();
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
            _subscriptionClient.RegisterMessageHandler((msg, token) =>
            {
                _logger?.LogDebug("Data received with message Id {0}", msg.MessageId);
                if (DataArrived == null)
                {
                    _logger?.LogDebug("Handler is null, skipping Invocation");
                }
                try
                {
                    DataArrived?.Invoke(this, msg.Body);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, ex.Message);
                }
                return Task.CompletedTask;
            }, messageHandlerOptions);
        }

        private void ReleaseUnmanagedResources()
        {
            _subscriptionClient?.CloseAsync().GetAwaiter().GetResult();
            _client?.CloseAsync().GetAwaiter().GetResult();
        }
    }
}