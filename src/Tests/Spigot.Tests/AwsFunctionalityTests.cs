using System.ComponentModel;
using System.Threading.Tasks;
using Amazon;
using Amazon.Kinesis;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Archetypical.Software.Spigot.Streams.AWS;
using DockerComposeFixture;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Spigot.Tests
{
    public class AwsFunctionalityTests : SpigotSerializerTests, IClassFixture<DockerFixture>
    {
        public AwsFunctionalityTests(DockerFixture dockerFixture, ITestOutputHelper outputHelper) : base(outputHelper)
        {
            dockerFixture.InitOnce(() => new DockerFixtureOptions
            {
                DockerComposeFiles = new[] { "docker-compose-aws.yml" },
            });
        }

        private async Task<KinesisStream> GetKinesisStream()
        {
            var stream = new KinesisStream(factory.CreateLogger<KinesisStream>());
            var settings = new KinesisSettings
            {
                ClientConfig = new AmazonKinesisConfig
                {
                    ServiceURL = "http://localhost:4568",
                    UseHttp = true,
                },
                Credentials = new SessionAWSCredentials("s", "d", "s"),
                StreamName = "test"
            };
            await stream.InitAsync(settings);
            return stream;
        }

        private async Task<SnsStream> GetSnsStream()
        {
            var stream = new SnsStream(factory.CreateLogger<SnsStream>());
            var credentials = new BasicAWSCredentials("s", "s");
            var snsStreamSettings = new SnsStreamSettings
            {
                AmazonSimpleNotificationServiceConfig = new AmazonSimpleNotificationServiceConfig
                {
                    ServiceURL = "http://localhost:4575",
                    UseHttp = true
                },
                AwsCredentials = credentials,
                TopicName = "test",
                CallbackSettings = new CallbackSettings
                {
                    Protocol = Protocol.Http,
                    Prefix = "http://ewassef.dyndns.org:8081/sns/"
                }
            };
            await stream.InitAsync(snsStreamSettings);
            return stream;
        }

        [Fact, Category("AWS")]
        public async Task AWS_Kinesis_Basic_Test()
        {
            using (var kinesisStream = await GetKinesisStream())
            {
                TestStream(kinesisStream);
            }
        }

        [Fact, Category("AWS")]
        public async Task AWS_Kinesis_Burst_Test()
        {
            using (var kinesisStream = await GetKinesisStream())
            {
                TestBurstStream(kinesisStream);
            }
        }

        [Fact, Category("AWS")]
        public async Task AWS_Kinesis_MultipleStreams_Test()
        {
            using (var kinesisStream1 = await GetKinesisStream())
            using (var kinesisStream2 = await GetKinesisStream())
            {
                TestMultipleInstancesOfStream(kinesisStream1, kinesisStream2);
            }
        }

        [FactAttribute]
        [Category("AWS")]
        public async Task AWS_Sns_Basic_Test()
        {
            using (var snsStream = await GetSnsStream())
            {
                TestStream(snsStream);
            }
        }

        [FactAttribute]
        [Category("AWS")]
        public async Task AWS_Sns_Burst_Test()
        {
            using (var snsStream = await GetSnsStream())
            {
                TestBurstStream(snsStream);
            }
        }
    }
}