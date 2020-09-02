using Archetypical.Software.Spigot.Streams.RabbitMq;
using DockerComposeFixture;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.ComponentModel;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Spigot.Tests
{
    public class RabbitMqIntegrationTests : SpigotSerializerTests, IClassFixture<DockerFixture>
    {
        public RabbitMqIntegrationTests(DockerFixture dockerFixture, ITestOutputHelper outputHelper) : base(outputHelper)
        {
            dockerFixture.InitOnce(() => new DockerFixtureOptions
            {
                DockerComposeFiles = new[] { "docker-compose-rabbitmq.yml" },
                CustomUpTest = s =>
                {
                    var client = new TcpClient();
                    try
                    {
                        client.Connect("localhost", 5672);
                        return client.Connected;
                    }
                    catch
                    {
                        return false;
                    }
                }
            });
        }

        [Fact]
        [Category("Rabbit MQ")]
        public async Task RabbitMQ_PubSub_Basic_Test()
        {
            var section = config.GetSection("RabbitMQ");
            using (var rabbitMq = await GetRabbitStreamAsync())
            {
                TestStream(rabbitMq);
            }
        }

        [Fact]
        [Category("Rabbit MQ")]
        public async Task RabbitMQ_PubSub_Burst_Test()
        {
            using (var rabbitMq = await GetRabbitStreamAsync())
            {
                TestBurstStream(rabbitMq);
            }
        }

        [Fact]
        [Category("Rabbit MQ")]
        public async Task RabbitMQ_PubSub_MultiStream_Test()
        {
            using (var rabbitMq1 = await GetRabbitStreamAsync())
            using (var rabbitMq2 = await GetRabbitStreamAsync())
            {
                TestMultipleInstancesOfStream(rabbitMq1, rabbitMq2);
            }
        }

        private async Task<RabbitMqStream> GetRabbitStreamAsync()
        {
            var stream = new RabbitMqStream(factory.CreateLogger<RabbitMqStream>());
            var settings = new RabbitMqSettings
            {
                ConnectionFactory = new ConnectionFactory
                ()
            };
            await stream.InitAsync(settings);
            return stream;
        }
    }
}