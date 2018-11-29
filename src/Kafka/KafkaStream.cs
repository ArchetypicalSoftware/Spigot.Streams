using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Archetypical.Software.Spigot.Streams.Kafka
{
    public class KafkaStream : ISpigotStream, IDisposable
    {
        private KafkaSettings _settings;

        private CancellationTokenSource cancellationTokenSource;

        private IConsumer<Null, byte[]> consumer;

        private IProducer<Null, byte[]> producer;

        private ILogger<KafkaStream> Logger;

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
                Logger?.LogDebug("Preparing to send {0} bytes", data.Length);
                var msg = new Message<Null, byte[]>() { Value = data };
                var result = producer.ProduceAsync(_settings.Topic.Name, msg).GetAwaiter().GetResult();
                Logger?.LogDebug("Message sending successful. Message sent to {0}", result.TopicPartitionOffset);
                return true;
            }
            catch (Exception e)
            {
                Logger?.LogDebug(e, "Error {0}", e.Message);
                return false;
            }
        }

        private async Task Init(KafkaSettings settings)
        {
            Logger = settings.Logger;
            cancellationTokenSource = new CancellationTokenSource();
            _settings = settings;
            producer = new Producer<Null, byte[]>(settings.ProducerConfig);
            producer.OnError += OnError;
            consumer = new Consumer<Null, byte[]>(settings.ConsumerConfig);
            consumer.OnError += OnError;
            //var client = new AdminClient(new AdminClientConfig { BootstrapServers = settings.ProducerConfig.BootstrapServers });

            //await client.CreateTopicsAsync(new List<TopicSpecification>{new TopicSpecification
            //{
            //    Name = settings.Topic.Name,
            //    NumPartitions = settings.Topic.NumberOfPartitions,
            //    ReplicationFactor = settings.Topic.Replicas,
            //}}, new CreateTopicsOptions
            //{
            //});

            consumer.Subscribe(settings.Topic.Name);
            Task.Factory.StartNew(PollerJob);
        }

        private void OnError(object sender, ErrorEvent e)
        {
            throw new ArgumentException(e.Reason, e.Code.GetReason());
        }

        private const int commitPeriod = 10;

        private void PollerJob()
        {
            Logger?.LogInformation("Starting to consume from the {0}", _settings.Topic.Name);
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var consumed = consumer.Consume(TimeSpan.FromMilliseconds(500));
                    if (consumed != null && !consumed.Offset.IsSpecial)
                    {
                        Logger?.LogDebug("Data arrived {0}", consumed.TopicPartitionOffset);
                        DataArrived?.Invoke(this, consumed.Value);

                        if (!_settings.ConsumerConfig.EnableAutoCommit.GetValueOrDefault()
                            && consumed.Offset.Value % commitPeriod == 0)
                        {
                            // The Commit method sends a "commit offsets" request to the Kafka
                            // cluster and synchronously waits for the response. This is very
                            // slow compared to the rate at which the consumer is capable of
                            // consuming messages. A high performance application will typically
                            // commit offsets relatively infrequently and be designed handle
                            // duplicate messages in the event of failure.
                            var committedOffsets = consumer.Commit(consumed, cancellationTokenSource.Token);
                            Logger?.LogDebug($"Committed offset: {0}", committedOffsets);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger?.LogDebug(e, "{0}", e.Message);
                    //
                }
            }
        }

        private void ReleaseUnmanagedResources()
        {
            cancellationTokenSource.Cancel();
            producer?.Flush(TimeSpan.FromSeconds(3));
            producer?.Dispose();
            consumer?.Dispose();
        }
    }
}