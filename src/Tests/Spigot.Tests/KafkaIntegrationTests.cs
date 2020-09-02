using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Archetypical.Software.Spigot.Streams.Kafka;
using Confluent.Kafka;
using DockerComposeFixture;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Spigot.Tests
{
    public class KafkaIntegrationTests : SpigotSerializerTests, IClassFixture<DockerFixture>
    {
        public KafkaIntegrationTests(DockerFixture dockerFixture, ITestOutputHelper helper) : base(helper)
        {
            dockerFixture.InitOnce(() => new DockerFixtureOptions
            {
                DockerComposeFiles = new[] { "docker-compose-kafka.yml" },
                CustomUpTest = s =>
                {
                    Thread.Sleep(1000);
                    var client = new TcpClient();
                    try
                    {
                        client.Connect("127.0.0.1", 9092);
                        return client.Connected;
                    }
                    catch
                    {
                        return false;
                    }
                }
            });
        }

        private async Task<KafkaStream> GetKafkaStreamAsync(string groupId = null)
        {
            var brokers = "127.0.0.1:9092";
            var settings = new KafkaSettings
            {
                Topic = new KafkaSettings.TopicMetadata
                {
                    Name = "test-topic",
                },
                ProducerConfig = new ProducerConfig
                {
                    BootstrapServers = brokers,
                },
            };
            settings.ConsumerConfig = new ConsumerConfig(settings.ProducerConfig)
            {
                GroupId = groupId ?? "Spigot-Tests",
                AutoOffsetReset = AutoOffsetReset.Latest,
                EnableAutoCommit = true,
                AutoCommitIntervalMs = 100
            };
            var stream = new KafkaStream(factory.CreateLogger<KafkaStream>());
            await stream.InitAsync(settings);
            return stream;
        }

        [Fact]
        [Category("Kafka")]
        public async Task Kafka_PubSub_Basic_Test()
        {
            using (var kafka = await GetKafkaStreamAsync())
            {
                TestStream(kafka);
            }
        }

        [Fact]
        [Category("Kafka")]
        public async Task Kafka_PubSub_Multiple_Test()
        {
            using (var kafka1 = await GetKafkaStreamAsync("FirstStream"))
            using (var kafka2 = await GetKafkaStreamAsync("SecondStream"))
            {
                TestMultipleInstancesOfStream(kafka1, kafka2);
            }
        }

        [Fact]
        [Category("Kafka")]
        public async Task Kafka_PubSub_Burst_Test()
        {
            using (var kafka = await GetKafkaStreamAsync())
            {
                TestBurstStream(kafka);
            }
        }
    }
}