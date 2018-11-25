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

        private IConsumer<Ignore, byte[]> consumer;

        private IProducer<Null, byte[]> producer;

        private KafkaStream()
        {
        }

        ~KafkaStream()
        {
            ReleaseUnmanagedResources();
        }

        public event EventHandler<byte[]> DataArrived;

        public async static Task<KafkaStream> BuildAsync(Action<KafkaSettings> builder)
        {
            var settings = new KafkaSettings();
            builder(settings);
            var instance = new KafkaStream();
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
                var waitHandle = new AutoResetEvent(false);
                bool success = false; ;
                Action<DeliveryReportResult<Null, byte[]>> handler = r =>
                {
                    success = !r.Error.IsError;
                    waitHandle.Set();
                };

                var msg = new Message<Null, byte[]>() { Value = data };
                producer.BeginProduce(_settings.Topic.Name, msg, handler);
                producer.Flush(TimeSpan.FromMilliseconds(1000));
                waitHandle.WaitOne(TimeSpan.FromMilliseconds(10000));
                return success;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private async Task Init(KafkaSettings settings)
        {
            cancellationTokenSource = new CancellationTokenSource();
            _settings = settings;
            producer = new Producer<Null, byte[]>(settings.ProducerConfig);
            producer.OnError += OnError;
            consumer = new Consumer<Ignore, byte[]>(settings.ConsumerConfig);
            consumer.OnError += OnError;
            //var client = new AdminClient(settings.ProducerConfig);

            //await client.CreateTopicsAsync(new List<TopicSpecification>{new TopicSpecification
            //{
            //    Name = settings.Topic.Name,
            //    NumPartitions = settings.Topic.NumberOfPartitions,
            //}}, new CreateTopicsOptions
            //{
            //});

            consumer.Subscribe(settings.Topic.Name);
            consumer?.Consume(TimeSpan.FromMilliseconds(100));

            Task.Run(() => PollerJob());
        }

        private void OnError(object sender, ErrorEvent e)
        {
            throw new ArgumentException(e.Reason, e.Code.GetReason());
        }

        private void PollerJob()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var consumed = consumer?.Consume(TimeSpan.FromMilliseconds(100));
                    if (consumed != null)
                    {
                        DataArrived?.Invoke(this, consumed.Value);
                    }
                }
                catch (Exception e)
                {
                    //
                }
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