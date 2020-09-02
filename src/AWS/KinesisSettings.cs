using Amazon.Kinesis;
using Amazon.Runtime;
using Microsoft.Extensions.Logging;

namespace Archetypical.Software.Spigot.Streams.AWS
{
    public class KinesisSettings
    {
        public AWSCredentials Credentials { get; set; }
        public AmazonKinesisConfig ClientConfig { get; set; }
        public string StreamName { get; set; } = "Archetypical.Software Spigot Stream For Kinesis";
        public int ShardCount { get; set; } = 2;
    }
}