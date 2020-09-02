using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka.Admin;

[assembly: InternalsVisibleTo("Spigot.Tests")]

namespace Archetypical.Software.Spigot.Streams.Kafka
{
    public class KafkaStream : ISpigotStream, IDisposable
    {
        private const int commitPeriod = 10;
        private KafkaSettings _settings;

        private CancellationTokenSource cancellationTokenSource;

        private IConsumer<Null, byte[]> consumer;

        private ILogger<KafkaStream> Logger;
        private IProducer<Null, byte[]> producer;

        internal KafkaStream(ILogger<KafkaStream> logger)
        {
            Logger = logger;
        }

        ~KafkaStream() => ReleaseUnmanagedResources();

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

        internal async Task InitAsync(KafkaSettings settings)
        {
            cancellationTokenSource = new CancellationTokenSource();
            _settings = settings;

            producer = new ProducerBuilder<Null, byte[]>(settings.ProducerConfig).SetErrorHandler(OnError).Build();

            consumer = new ConsumerBuilder<Null, byte[]>(settings.ConsumerConfig).SetErrorHandler(OnError).Build();

            var client = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = settings.ProducerConfig.BootstrapServers }).Build();

            try
            {
                await client.CreateTopicsAsync(new List<TopicSpecification>{new TopicSpecification
            {
                Name = settings.Topic.Name,
                NumPartitions = settings.Topic.NumberOfPartitions,
                ReplicationFactor = settings.Topic.Replicas,
            }}, new CreateTopicsOptions
            {
            });
            }
            catch (CreateTopicsException)
            {
                Logger.LogInformation("Topic already exists... continuing");
            }

            consumer.Subscribe(settings.Topic.Name);
            _ = Task.Factory.StartNew(PollerJob, cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void OnError(object sender, Error e)
        {
            var eventId = new EventId((int)e.Code, e.Code.GetReason());
            Logger.LogError(eventId, e.Reason);
        }

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
                            consumer.Commit(consumed);
                            Logger?.LogDebug($"Committed offset: {consumed.Offset.Value}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger?.LogDebug(e, "{0}", e.Message);
                }
            }
        }

        private void ReleaseUnmanagedResources()
        {
            producer?.Flush(TimeSpan.FromSeconds(3));
            cancellationTokenSource.Cancel();
            producer?.Dispose();
            consumer?.Dispose();
        }
    }
}