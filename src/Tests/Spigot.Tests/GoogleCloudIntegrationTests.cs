using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Archetypical.Software.Spigot.Streams.GoogleCloud;
using DockerComposeFixture;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Spigot.Tests
{
    public class GoogleCloudIntegrationTests : SpigotSerializerTests, IClassFixture<DockerFixture>
    {
        public GoogleCloudIntegrationTests(DockerFixture dockerFixture, ITestOutputHelper outputHelper) : base(outputHelper)
        {
            dockerFixture.InitOnce(() => new DockerFixtureOptions
            {
                DockerComposeFiles = new[] { "docker-compose-googlecloud.yml" },
            });
        }

        private async Task<GoogleCloudPubSubStream> GetGoogleCloudPubSubStreamAsync()
        {
            var stream = new GoogleCloudPubSubStream(factory.CreateLogger<GoogleCloudPubSubStream>());
            var settings = new GoogleCloudSettings
            {
                ProjectId = "ArchetypicalSoftwareIntegrationTests",
                Channel = new Channel("localhost:8085", ChannelCredentials.Insecure)
            };
            await stream.InitAsync(settings);
            return stream;
        }

        [Fact]
        [Category("Google Cloud")]
        public async Task Google_Cloud_Basic_Test()
        {
            using (var googleCloud = await GetGoogleCloudPubSubStreamAsync())
            {
                TestStream(googleCloud);
            }
        }

        [Fact]
        [Category("Google Cloud")]
        public async Task Google_Cloud_Burst_Test()
        {
            using (var googleCloud = await GetGoogleCloudPubSubStreamAsync())
            {
                {
                    TestBurstStream(googleCloud);
                }
            }
        }

        [Fact]
        [Category("Google Cloud")]
        public async Task Google_Cloud_MultipleStream_Test()
        {
            using (var googleCloud1 = await GetGoogleCloudPubSubStreamAsync())
            using (var googleCloud2 = await GetGoogleCloudPubSubStreamAsync())
            {
                TestMultipleInstancesOfStream(googleCloud1, googleCloud2);
            }
        }
    }
}