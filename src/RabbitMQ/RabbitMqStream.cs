using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Archetypical.Software.Spigot.Streams.RabbitMq
{
    public class RabbitMqStream : ISpigotStream, IDisposable
    {
        private CancellationTokenSource _cancellationTokenSource;
        private RabbitMqSettings _settings;

        private IModel channel;

        private IConnection connection;

        private EventingBasicConsumer consumer;

        private RabbitMqStream()
        {
        }

        ~RabbitMqStream()
        {
            ReleaseUnmanagedResources();
        }

        public event EventHandler<byte[]> DataArrived;

        public static async Task<RabbitMqStream> BuildAsync(Action<RabbitMqSettings> builder)
        {
            var settings = new RabbitMqSettings();
            builder(settings);
            var instance = new RabbitMqStream();
            await instance.Init(settings);
            return instance;
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        public bool TrySend(byte[] data)
        {
            try
            {
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;
                channel.BasicPublish(exchange: "",
                    routingKey: _settings.Queue.Name,
                    basicProperties: properties,
                    body: data);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task Init(RabbitMqSettings settings)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _settings = settings;
            var factory = settings.ConnectionFactory;

            connection = factory.CreateConnection();

            channel = connection.CreateModel();
            channel.QueueDeclare(queue: settings.Queue.Name, durable: settings.Queue.Durable,
                exclusive: settings.Queue.Exclusive, autoDelete: settings.Queue.AutoDelete,
                arguments: settings.Queue.Arguments);
            channel.BasicQos(prefetchSize: settings.Qos.PrefetchSize, prefetchCount: settings.Qos.PrefetchCount, global: settings.Qos.Global);

            consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                DataArrived?.Invoke(this, ea.Body);

                channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            };
            channel.BasicConsume(queue: settings.Queue.Name,
                autoAck: false,
                consumer: consumer);
        }

        private void ReleaseUnmanagedResources()
        {
            channel?.Dispose();
            connection?.Dispose();
        }
    }
}