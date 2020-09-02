using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Archetypical.Software.Spigot.Streams.Redis;
using DockerComposeFixture;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Spigot.Tests
{
    public class RedisIntegrationTests : SpigotSerializerTests, IClassFixture<DockerFixture>
    {
        public RedisIntegrationTests(DockerFixture dockerFixture, ITestOutputHelper outputHelper) : base(outputHelper)
        {
            dockerFixture.InitOnce(() => new DockerFixtureOptions
            {
                DockerComposeFiles = new[] { "docker-compose-redis.yml" },
                CustomUpTest = s =>
                {
                    var client = new TcpClient();
                    try
                    {
                        client.Connect("localhost", 6379);
                        return client.Connected;
                    }
                    catch
                    {
                        return false;
                    }
                }
            });
        }

        private async Task<RedisStream> GetRedisStreamAsync()
        {
            var stream = new RedisStream(factory.CreateLogger<RedisStream>());
            var settings = new RedisSettings
            {
                ConfigurationOptions = StackExchange.Redis.ConfigurationOptions.Parse("localhost")
            };
            await stream.InitAsync(settings);
            return stream;
        }

        [Fact]
        [Category("Redis")]
        public async Task Redis_PubSub_Basic_Test()
        {
            using (var redis = await GetRedisStreamAsync())
            {
                TestStream(redis);
            }
        }

        [Fact]
        [Category("Redis")]
        public async Task Redis_PubSub_Burst_Test()
        {
            using (var redis = await GetRedisStreamAsync())
            {
                TestBurstStream(redis);
            }
        }

        [Fact]
        [Category("Redis")]
        public async Task Redis_PubSub_MultipleStream_Test()
        {
            using (var redis1 = await GetRedisStreamAsync())
            using (var redis2 = await GetRedisStreamAsync())
            {
                TestMultipleInstancesOfStream(redis1, redis2);
            }
        }
    }
}