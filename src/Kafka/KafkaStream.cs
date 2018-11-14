using Confluent.Kafka;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Archetypical.Software.Spigot.Streams.Kafka
{
    public class KafkaStream : ISpigotStream, IDisposable
    {
        private KafkaSettings _settings;

        private CancellationTokenSource cancellationTokenSource;

        private Consumer consumer;

        private Producer producer;

        private KafkaStream()
        {
        }

        ~KafkaStream()
        {
            ReleaseUnmanagedResources();
        }

        public event EventHandler<byte[]> DataArrived;

        public static KafkaStream Build(Action<KafkaSettings> builder)
        {
            var settings = new KafkaSettings();
            builder(settings);
            var instance = new KafkaStream();
            instance.Init(settings);
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
                producer.ProduceAsync(_settings.TopicName, null, data).GetAwaiter().GetResult();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Init(KafkaSettings settings)
        {
            cancellationTokenSource = new CancellationTokenSource();
            _settings = settings;
            producer = new Producer(settings.ProducerConfig, settings.ManualPoll, settings.DisableDeliveryReports);
            consumer = new Consumer(settings.Consumeronfig);
            consumer.OnMessage += (o, e) => DataArrived?.Invoke(this, e.Value); ;
            consumer.Subscribe(settings.TopicName);
            Task.Run(() => PollerJob());
        }

        private void PollerJob()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                consumer?.Poll(100);
            }
        }

        private void ReleaseUnmanagedResources()
        {
            cancellationTokenSource.Cancel();
            producer?.Dispose();
            consumer?.Dispose();
        }
    }
}