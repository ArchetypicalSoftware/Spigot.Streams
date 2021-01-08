using System.ComponentModel;
using System.Net.Sockets;
using System.Threading.Tasks;
using Amazon;
using Amazon.Kinesis;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Archetypical.Software.Spigot.Streams.AWS;
using Archetypical.Software.Spigot.Streams.KubeMQ;
using DockerComposeFixture;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Spigot.Tests
{
    public class KubeMqFunctionalityTests : SpigotSerializerTests, IClassFixture<DockerFixture>
    {
        public KubeMqFunctionalityTests(DockerFixture dockerFixture, ITestOutputHelper outputHelper) : base(outputHelper)
        {
            dockerFixture.InitOnce(() => new DockerFixtureOptions
            {
                DockerComposeFiles = new[] { "docker-compose-kubemq.yml" },
                CustomUpTest = s =>
                {
                    var client = new TcpClient();
                    try
                    {
                        client.Connect("localhost", 9090);
                        if (client.Connected)
                            Task.Delay(2000).GetAwaiter().GetResult();
                        return client.Connected;
                    }
                    catch
                    {
                        return false;
                    }
                }
            });
        }

        private async Task<KubeMqStream> GetKubeMQStream()
        {
            var stream = new KubeMqStream(factory.CreateLogger<KubeMqStream>());
            var settings = new KubeMqSettings
            {
                ChannelName = "integration-channel",
                ServerAddress = "127.0.0.1:50000",
                EventsStoreType = KubeMQ.SDK.csharp.Subscription.EventsStoreType.StartFromFirst,
                Group = "IntegrationTests",
                SubscriptionType = KubeMQ.SDK.csharp.Subscription.SubscribeType.EventsStore,
            };
            await stream.InitAsync(settings);
            await Task.Delay(500);
            return stream;
        }

        [Fact, Category("KubeMQ")]
        public async Task KubeMq_Basic_Test()
        {
            using (var kinesisStream = await GetKubeMQStream())
            {
                TestStream(kinesisStream);
            }
        }

        [Fact, Category("KubeMQ")]
        public async Task KubeMq_Burst_Test()
        {
            using (var kinesisStream = await GetKubeMQStream())
            {
                TestBurstStream(kinesisStream);
            }
        }

        [Fact, Category("KubeMQ")]
        public async Task KubeMq_MultipleStreams_Test()
        {
            using (var kinesisStream1 = await GetKubeMQStream())
            using (var kinesisStream2 = await GetKubeMQStream())
            {
                //await Task.Delay(1000);
                TestMultipleInstancesOfStream(kinesisStream1, kinesisStream2);
            }
        }
    }
}