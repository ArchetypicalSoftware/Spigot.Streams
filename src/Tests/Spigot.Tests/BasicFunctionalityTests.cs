using Amazon;
using Amazon.Kinesis;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Archetypical.Software.Spigot;
using Archetypical.Software.Spigot.Streams.AWS;
using Archetypical.Software.Spigot.Streams.Azure;
using Archetypical.Software.Spigot.Streams.GoogleCloud;
using Archetypical.Software.Spigot.Streams.Kafka;
using Archetypical.Software.Spigot.Streams.RabbitMq;
using Confluent.Kafka;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Stream = Archetypical.Software.Spigot.Streams.Redis.Stream;

namespace Spigot.Tests
{
    public class BasicFunctionalityTests
    {
        public BasicFunctionalityTests(ITestOutputHelper outputHelper)
        {
            factory = new LoggerFactory();
            factory.AddProvider(new XunitLoggerProvider(outputHelper));

            Debug.Listeners.Add(new DefaultTraceListener());
            Archetypical.Software.Spigot.Spigot.Setup(settings => { settings.AddLoggerFactory(factory); });
            logger = factory.CreateLogger<BasicFunctionalityTests>();

            config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json", true, false)
                .AddEnvironmentVariables("SPIGOT_")
                //In environment variables, a colon separator may not work on all platforms. A double underscore (__) is supported by all platforms and is converted to a colon.
                .Build();
        }

        private readonly ILoggerFactory factory;
        private static ILogger logger;
        private readonly IConfigurationRoot config;

        private static void TestStream(ISpigotStream stream)
        {
            Assert.NotNull(stream);
            Thread.Sleep(100);
            var expected = Guid.NewGuid();
            var dataToSend = expected.ToByteArray();
            var actual = Guid.Empty;
            var signal = new AutoResetEvent(false);
            stream.DataArrived += (sender, bytes) =>
            {
                logger.LogDebug(new Guid(bytes).ToString());
                if (new Guid(bytes) != expected)
                {
                    return;
                }

                actual = new Guid(bytes);
                signal.Set();
            };
            Thread.Sleep(100);
            var sw = Stopwatch.StartNew();
            Assert.True(stream.TrySend(dataToSend), "Failed to send message on the stream");
            signal.WaitOne(TimeSpan.FromSeconds(60));
            sw.Stop();
            logger.Log(LogLevel.Information, "Roundtrip by {1} in {0}", sw.Elapsed, stream.GetType().FullName);
            Assert.Equal(expected, actual);
        }

        private static void TestBurstStream(ISpigotStream stream, int messagesToSend = 10)
        {
            Assert.NotNull(stream);
            Thread.Sleep(100);
            var expected = Enumerable.Range(0, messagesToSend).Select(x => Guid.NewGuid()).ToList();
            // Send X messages and wait up to .25 sec per message

            var dataToSend = expected.Select(x => x.ToByteArray());

            var signal = new AutoResetEvent(false);
            var matched = 0;
            stream.DataArrived += (sender, bytes) =>
            {
                var arrived = new Guid(bytes);
                if (expected.Contains(arrived))
                {
                    Interlocked.Increment(ref matched);
                }

                if (matched == messagesToSend)
                {
                    signal.Set();
                }
            };
            Thread.Sleep(100);
            var sw = Stopwatch.StartNew();
            foreach (var message in dataToSend)
            {
                Assert.True(stream.TrySend(message), "Failed to send message on the stream");
            }

            signal.WaitOne(TimeSpan.FromMilliseconds(250 * messagesToSend));
            sw.Stop();
            logger.Log(LogLevel.Information, "Roundtrip by {1} in {0}", sw.Elapsed, stream.GetType().FullName);
            Assert.Equal(messagesToSend, matched);
        }

        private static void TestMultipleInstancesOfStream(ISpigotStream streamInstance1, ISpigotStream streamInstance2)
        {
            Assert.NotNull(streamInstance1);
            Assert.NotNull(streamInstance2);
            var expected1 = Guid.NewGuid();
            var expected2 = Guid.NewGuid();
            var dataToSend1 = expected1.ToByteArray();
            var dataToSend2 = expected2.ToByteArray();
            var actual1 = Guid.Empty;
            var actual2 = Guid.Empty;
            var signal = new AutoResetEvent(false);

            void OnStreamInstance2OnDataArrived(object sender, byte[] bytes)
            {
                logger.LogDebug($"{new Guid(bytes)} arrived on stream 2");
                if (new Guid(bytes) != expected2)
                {
                    return;
                }

                actual2 = new Guid(bytes);
                signal.Set();
            }

            void OnStreamInstance1OnDataArrived(object sender, byte[] bytes)
            {
                logger.LogDebug($"{new Guid(bytes)} arrived on stream 1");
                if (new Guid(bytes) != expected1)
                {
                    return;
                }

                actual1 = new Guid(bytes);
                signal.Set();
            }

            streamInstance1.DataArrived += OnStreamInstance1OnDataArrived;
            streamInstance2.DataArrived += OnStreamInstance2OnDataArrived;

            Thread.Sleep(100);
            var sw = Stopwatch.StartNew();
            Assert.True(streamInstance2.TrySend(dataToSend1));
            signal.WaitOne(TimeSpan.FromSeconds(3));
            sw.Stop();
            logger.Log(LogLevel.Information, "Roundtrip by {1} in {0}", sw.Elapsed, streamInstance1.GetType().FullName);
            Assert.Equal(expected1, actual1);

            signal.Reset();
            Thread.Sleep(100);

            sw = Stopwatch.StartNew();
            Assert.True(streamInstance1.TrySend(dataToSend2));
            signal.WaitOne(TimeSpan.FromSeconds(3));
            sw.Stop();
            logger.Log(LogLevel.Information, "Roundtrip by {1} in {0}", sw.Elapsed, streamInstance2.GetType().FullName);
            Assert.Equal(expected2, actual2);
        }

        [Fact]
        [Category("AWS")]
        public async Task AWS_Kinesis_Basic_Test()
        {
            var settings = config.GetSection("AWS");
            var accessToken = settings.GetValue<string>("AccessToken");
            var secretKey = settings.GetValue<string>("SecretKey");
            var credentials = new BasicAWSCredentials(accessToken, secretKey);
            using (var kinesisStream = await KinesisStream.BuildAsync(builder =>
            {
                builder.ClientConfig = new AmazonKinesisConfig
                {
                    RegionEndpoint = RegionEndpoint.USEast1,
                    MaxErrorRetry = 10
                };
                builder.Credentials = credentials;
                builder.StreamName = "test";
                builder.Logger = factory.CreateLogger<KinesisStream>();
            }))
            {
                TestStream(kinesisStream);
            }
        }

        [Fact]
        [Category("AWS")]
        public async Task AWS_Kinesis_Burst_Test()
        {
            var settings = config.GetSection("AWS");
            var accessToken = settings.GetValue<string>("AccessToken");
            var secretKey = settings.GetValue<string>("SecretKey");
            var credentials = new BasicAWSCredentials(accessToken, secretKey);
            using (var kinesisStream = await KinesisStream.BuildAsync(builder =>
            {
                builder.ClientConfig = new AmazonKinesisConfig
                {
                    RegionEndpoint = RegionEndpoint.USEast1,
                    MaxErrorRetry = 10
                };
                builder.Credentials = credentials;
                builder.StreamName = "test";
                builder.Logger = factory.CreateLogger<KinesisStream>();
            }))
            {
                TestBurstStream(kinesisStream);
            }
        }

        [Fact]
        [Category("AWS")]
        public async Task AWS_Kinesis_MultipleStreams_Test()
        {
            var settings = config.GetSection("AWS");
            var accessToken = settings.GetValue<string>("AccessToken");
            var secretKey = settings.GetValue<string>("SecretKey");
            var credentials = new BasicAWSCredentials(accessToken, secretKey);
            using (var kinesisStream1 = await KinesisStream.BuildAsync(builder =>
            {
                builder.ClientConfig = new AmazonKinesisConfig
                {
                    RegionEndpoint = RegionEndpoint.USEast1,
                    MaxErrorRetry = 10
                };
                builder.Credentials = credentials;
                builder.StreamName = "test";
                builder.Logger = factory.CreateLogger<KinesisStream>();
            }))
            using (var kinesisStream2 = await KinesisStream.BuildAsync(builder =>
            {
                builder.ClientConfig = new AmazonKinesisConfig
                {
                    RegionEndpoint = RegionEndpoint.USEast1,
                    MaxErrorRetry = 10
                };
                builder.Credentials = credentials;
                builder.StreamName = "test";
                builder.Logger = factory.CreateLogger<KinesisStream>();
            }))
            {
                TestMultipleInstancesOfStream(kinesisStream1, kinesisStream2);
            }
        }

        [Fact]
        [Category("AWS")]
        public async Task AWS_Sns_Basic_Test()
        {
            var settings = config.GetSection("AWS");
            var accessToken = settings.GetValue<string>("AccessToken");
            var secretKey = settings.GetValue<string>("SecretKey");
            var credentials = new BasicAWSCredentials(accessToken, secretKey);
            using (var snsStream = await SnsStream.BuildAsync(builder =>
            {
                builder.AmazonSimpleNotificationServiceConfig = new AmazonSimpleNotificationServiceConfig
                {
                    RegionEndpoint = RegionEndpoint.USEast1,
                    MaxErrorRetry = 10,
                    UseHttp = true,
                    SignatureMethod = SigningAlgorithm.HmacSHA256,
                    SignatureVersion = "1"
                };
                builder.AwsCredentials = credentials;
                builder.TopicName = "test";
                builder.CallbackSettings.Protocol = Protocol.Http;
                builder.CallbackSettings.Prefix = "http://ewassef.dyndns.org:8081/sns/";
            }))
            {
                TestStream(snsStream);
            }
        }

        [Fact]
        [Category("AWS")]
        public async Task AWS_Sns_Burst_Test()
        {
            var settings = config.GetSection("AWS");
            var accessToken = settings.GetValue<string>("AccessToken");
            var secretKey = settings.GetValue<string>("SecretKey");
            var credentials = new BasicAWSCredentials(accessToken, secretKey);
            using (var snsStream = await SnsStream.BuildAsync(builder =>
            {
                builder.AmazonSimpleNotificationServiceConfig = new AmazonSimpleNotificationServiceConfig
                {
                    RegionEndpoint = RegionEndpoint.USEast1,
                    MaxErrorRetry = 10,
                    UseHttp = true,
                    SignatureMethod = SigningAlgorithm.HmacSHA256,
                    SignatureVersion = "1"
                };
                builder.AwsCredentials = credentials;
                builder.TopicName = "test";
                builder.CallbackSettings.Protocol = Protocol.Http;
                builder.CallbackSettings.Prefix = "http://ewassef.dyndns.org:8081/sns/";
            }))
            {
                TestBurstStream(snsStream);
            }
        }

        [Fact]
        [Category("Azure")]
        public async Task Azure_ServiceBus_Basic_Test()
        {
            var section = config.GetSection("Azure");
            using (var azureStream = await AzureServiceBusStream.BuildAsync(builder =>
            {
                builder.Logger = factory.CreateLogger<AzureServiceBusStream>();
                builder.TopicName = section.GetValue<string>("TopicName");
                builder.ConnectionStringBuilder = new ServiceBusConnectionStringBuilder
                {
                    SasKeyName = section.GetValue<string>("SasKeyName"),
                    SasKey = section.GetValue<string>("SasKey"),
                    TransportType = TransportType.Amqp,
                    Endpoint = section.GetValue<string>("Endpoint")
                };
            }))
            {
                TestStream(azureStream);
            }
        }

        [Fact]
        [Category("Azure")]
        public async Task Azure_ServiceBus_Burst_Test()
        {
            var section = config.GetSection("Azure");
            using (var azureStream = await AzureServiceBusStream.BuildAsync(builder =>
            {
                builder.Logger = factory.CreateLogger<AzureServiceBusStream>();
                builder.TopicName = section.GetValue<string>("TopicName");
                builder.ConnectionStringBuilder = new ServiceBusConnectionStringBuilder
                {
                    SasKeyName = section.GetValue<string>("SasKeyName"),
                    SasKey = section.GetValue<string>("SasKey"),
                    TransportType = TransportType.Amqp,
                    Endpoint = section.GetValue<string>("Endpoint"),
                };
            }))
            {
                TestBurstStream(azureStream);
            }
        }

        [Fact]
        [Category("Azure")]
        public async Task Azure_ServiceBus_Multiple_Test()
        {
            var section = config.GetSection("Azure");
            using (var azureStream1 = await AzureServiceBusStream.BuildAsync(builder =>
            {
                builder.Logger = factory.CreateLogger<AzureServiceBusStream>();
                builder.TopicName = section.GetValue<string>("TopicName");
                builder.ConnectionStringBuilder = new ServiceBusConnectionStringBuilder
                {
                    SasKeyName = section.GetValue<string>("SasKeyName"),
                    SasKey = section.GetValue<string>("SasKey"),
                    TransportType = TransportType.Amqp,
                    Endpoint = section.GetValue<string>("Endpoint")
                };
            }))
            using (var azureStream2 = await AzureServiceBusStream.BuildAsync(builder =>
            {
                builder.Logger = factory.CreateLogger<AzureServiceBusStream>();
                builder.TopicName = section.GetValue<string>("TopicName");
                builder.ConnectionStringBuilder = new ServiceBusConnectionStringBuilder
                {
                    SasKeyName = section.GetValue<string>("SasKeyName"),
                    SasKey = section.GetValue<string>("SasKey"),
                    TransportType = TransportType.Amqp,
                    Endpoint = section.GetValue<string>("Endpoint")
                };
            }))
            {
                TestMultipleInstancesOfStream(azureStream1, azureStream2);
            }
        }

        [Fact]
        [Category("Google Cloud")]
        public async Task Google_Cloud_Basic_Test()
        {
            var section = config.GetSection("GoogleCloud");
            logger.Log(LogLevel.Information,
                "Google cloud can use a config json stored in an env variable GOOGLE_APPLICATION_CREDENTIALS. That value is {0}",
                Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"));
            using (var googleCloud = await GoogleCloudPubSubStream.BuildAsync(settings =>
            {
                settings.ProjectId = section.GetValue<string>("ProjectId");
                settings.Logger = factory.CreateLogger<GoogleCloudPubSubStream>();
            }))
            {
                {
                    TestStream(googleCloud);
                }
            }
        }

        [Fact]
        [Category("Google Cloud")]
        public async Task Google_Cloud_Burst_Test()
        {
            var section = config.GetSection("GoogleCloud");
            logger.Log(LogLevel.Information,
                "Google cloud can use a config json stored in an env variable GOOGLE_APPLICATION_CREDENTIALS. That value is {0}",
                Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"));
            using (var googleCloud = await GoogleCloudPubSubStream.BuildAsync(settings =>
            {
                settings.ProjectId = section.GetValue<string>("ProjectId");
                settings.Logger = factory.CreateLogger<GoogleCloudPubSubStream>();
            }))
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
            var section = config.GetSection("GoogleCloud");
            logger.Log(LogLevel.Information,
                "Google cloud can use a config json stored in an env variable GOOGLE_APPLICATION_CREDENTIALS. That value is {0}",
                Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"));
            using (var googleCloud1 = await GoogleCloudPubSubStream.BuildAsync(settings =>
            {
                settings.ProjectId = section.GetValue<string>("ProjectId");
                settings.Logger = factory.CreateLogger<GoogleCloudPubSubStream>();
            }))
            using (var googleCloud2 = await GoogleCloudPubSubStream.BuildAsync(settings =>
            {
                settings.ProjectId = section.GetValue<string>("ProjectId");
                settings.Logger = factory.CreateLogger<GoogleCloudPubSubStream>();
            }))
            {
                TestMultipleInstancesOfStream(googleCloud1, googleCloud2);
            }
        }

        [Fact]
        [Category("Kafka")]
        public async Task Kafka_PubSub_Basic_Test()
        {
            var section = config.GetSection("Kafka");
            using (var kafka = await KafkaStream.BuildAsync(settings =>
            {
                settings.Logger = factory.CreateLogger<KafkaStream>();
                var brokers = section.GetValue<string>("Brokers");
                settings.Topic.Name = section.GetValue<string>("TopicName");
                settings.ProducerConfig.BootstrapServers = brokers;
                settings.ProducerConfig.SaslUsername = section.GetValue<string>("UserName");
                settings.ProducerConfig.SaslPassword = section.GetValue<string>("Password");
                settings.ProducerConfig.SaslMechanism = SaslMechanismType.ScramSha256;
                settings.ProducerConfig.SecurityProtocol = SecurityProtocolType.Sasl_Ssl;
                var cert = new FileInfo("cacert.pem");
                if (cert.Exists)
                {
                    settings.ProducerConfig.SslCaLocation = cert.FullName;
                }

                settings.ConsumerConfig = new ConsumerConfig(settings.ProducerConfig)
                {
                    GroupId = Guid.NewGuid().ToString(),
                    AutoOffsetReset = AutoOffsetResetType.Earliest,
                    EnableAutoCommit = false,
                    AutoCommitIntervalMs = 100
                };
            }))
            {
                TestStream(kafka);
            }
        }

        [Fact]
        [Category("Kafka")]
        public async Task Kafka_PubSub_Burst_Test()
        {
            var section = config.GetSection("Kafka");
            using (var kafka = await KafkaStream.BuildAsync(settings =>
            {
                settings.Logger = factory.CreateLogger<KafkaStream>();
                var brokers = section.GetValue<string>("Brokers");
                settings.Topic.Name = section.GetValue<string>("TopicName");
                settings.ProducerConfig.BootstrapServers = brokers;
                settings.ProducerConfig.SaslUsername = section.GetValue<string>("UserName");
                settings.ProducerConfig.SaslPassword = section.GetValue<string>("Password");

                settings.ProducerConfig.SaslMechanism = SaslMechanismType.ScramSha256;
                settings.ProducerConfig.SecurityProtocol = SecurityProtocolType.Sasl_Ssl;
                var cert = new FileInfo("cacert.pem");
                if (cert.Exists)
                {
                    settings.ProducerConfig.SslCaLocation = cert.FullName;
                }

                settings.ConsumerConfig = new
                    ConsumerConfig(settings.ProducerConfig)
                {
                    GroupId = Guid.NewGuid().ToString(),
                    AutoOffsetReset = AutoOffsetResetType.Latest,
                    EnableAutoCommit = true
                };
            }))
            {
                TestBurstStream(kafka);
            }
        }

        [Fact]
        [Category("Rabbit MQ")]
        public async Task RabbitMQ_PubSub_Basic_Test()
        {
            var section = config.GetSection("RabbitMQ");
            using (var rabbitMq =
                await RabbitMqStream.BuildAsync(settings =>
                {
                    settings.ConnectionFactory = new ConnectionFactory
                    {
                        HostName = section.GetValue<string>("Hostname"),
                        UserName = section.GetValue<string>("Username"),
                        Password = section.GetValue<string>("Password"),
                        VirtualHost = section.GetValue<string>("VirtualHost")
                    };
                }))
            {
                TestStream(rabbitMq);
            }
        }

        [Fact]
        [Category("Rabbit MQ")]
        public async Task RabbitMQ_PubSub_Burst_Test()
        {
            var section = config.GetSection("RabbitMQ");
            using (var rabbitMq =
                await RabbitMqStream.BuildAsync(settings =>
                {
                    settings.ConnectionFactory = new ConnectionFactory
                    {
                        HostName = section.GetValue<string>("Hostname"),
                        UserName = section.GetValue<string>("Username"),
                        Password = section.GetValue<string>("Password"),
                        VirtualHost = section.GetValue<string>("VirtualHost")
                    };
                }))
            {
                TestBurstStream(rabbitMq);
            }
        }

        [Fact]
        [Category("Rabbit MQ")]
        public async Task RabbitMQ_PubSub_MultiStream_Test()
        {
            var section = config.GetSection("RabbitMQ");
            using (var rabbitMq1 =
                await RabbitMqStream.BuildAsync(settings =>
                {
                    settings.ConnectionFactory = new ConnectionFactory
                    {
                        HostName = section.GetValue<string>("Hostname"),
                        UserName = section.GetValue<string>("Username"),
                        Password = section.GetValue<string>("Password"),
                        VirtualHost = section.GetValue<string>("VirtualHost")
                    };
                }))
            using (var rabbitMq2 =
                await RabbitMqStream.BuildAsync(settings =>
                {
                    settings.ConnectionFactory = new ConnectionFactory
                    {
                        HostName = section.GetValue<string>("Hostname"),
                        UserName = section.GetValue<string>("Username"),
                        Password = section.GetValue<string>("Password"),
                        VirtualHost = section.GetValue<string>("VirtualHost")
                    };
                }))
            {
                TestMultipleInstancesOfStream(rabbitMq1, rabbitMq2);
            }
        }

        [Fact]
        [Category("Redis")]
        public async Task Redis_PubSub_Basic_Test()
        {
            var section = config.GetSection("Redis");
            using (var redis = await Stream.BuildAsync(settings =>
            {
                settings.ConfigurationOptions.EndPoints.Add(section.GetValue<string>("Endpoint"));
                settings.ConfigurationOptions.Password = section.GetValue<string>("Password");
            }))
            {
                TestStream(redis);
            }
        }

        [Fact]
        [Category("Redis")]
        public async Task Redis_PubSub_Burst_Test()
        {
            var section = config.GetSection("Redis");
            using (var redis = await Stream.BuildAsync(settings =>
            {
                settings.ConfigurationOptions.EndPoints.Add(section.GetValue<string>("Endpoint"));
                settings.ConfigurationOptions.Password = section.GetValue<string>("Password");
            }))
            {
                TestBurstStream(redis);
            }
        }

        [Fact]
        [Category("Redis")]
        public async Task Redis_PubSub_MultipleStream_Test()
        {
            var section = config.GetSection("Redis");
            using (var redis1 = await Stream.BuildAsync(settings =>
            {
                settings.ConfigurationOptions.EndPoints.Add(section.GetValue<string>("Endpoint"));
                settings.ConfigurationOptions.Password = section.GetValue<string>("Password");
            }))
            using (var redis2 = await Stream.BuildAsync(settings =>
            {
                settings.ConfigurationOptions.EndPoints.Add(section.GetValue<string>("Endpoint"));
                settings.ConfigurationOptions.Password = section.GetValue<string>("Password");
            }))
            {
                TestMultipleInstancesOfStream(redis1, redis2);
            }
        }

        [Fact]
        [Category("ZeroMQ")]
        public async Task ZeroMQ_PubSub_Basic_Test()
        {
            using (XpubXsubIntermediary.Start())
            {
                using (var netmq = await Archetypical.Software.Spigot.Streams.ZeroMQ.Stream.BuildAsync(settings =>
                {
                    settings.XPublisherSocketConnectionString = @"tcp://127.0.0.1:1234";
                    settings.XSubscriberSocketConnectionString = @"tcp://127.0.0.1:5677";
                }))
                {
                    TestStream(netmq);
                }
            }
        }

        [Fact]
        [Category("ZeroMQ")]
        public async Task ZeroMQ_PubSub_Burst_Test()
        {
            using (XpubXsubIntermediary.Start())
            {
                using (var netmq = await Archetypical.Software.Spigot.Streams.ZeroMQ.Stream.BuildAsync(settings =>
                {
                    settings.XPublisherSocketConnectionString = @"tcp://127.0.0.1:1234";
                    settings.XSubscriberSocketConnectionString = @"tcp://127.0.0.1:5677";
                }))
                {
                    TestBurstStream(netmq);
                }
            }
        }

        [Fact]
        [Category("ZeroMQ")]
        public async Task ZeroMQ_PubSub_MultipleStream_Test()
        {
            using (XpubXsubIntermediary.Start())
            {
                await Task.Delay(500);
                using (var netmq1 = await Archetypical.Software.Spigot.Streams.ZeroMQ.Stream.BuildAsync(settings =>
                {
                    settings.XPublisherSocketConnectionString = @"tcp://127.0.0.1:1234";
                    settings.XSubscriberSocketConnectionString = @"tcp://127.0.0.1:5677";
                }))
                {
                    using (var netmq2 = await Archetypical.Software.Spigot.Streams.ZeroMQ.Stream.BuildAsync(settings =>
                    {
                        settings.XPublisherSocketConnectionString = @"tcp://127.0.0.1:1234";
                        settings.XSubscriberSocketConnectionString = @"tcp://127.0.0.1:5677";
                    }))
                    {
                        TestMultipleInstancesOfStream(netmq1, netmq2);
                    }
                }
            }
        }
    }
}