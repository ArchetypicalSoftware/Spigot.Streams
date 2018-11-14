using Amazon;
using Amazon.Kinesis;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Archetypical.Software.Spigot;
using Archetypical.Software.Spigot.Streams.AWS;
using Archetypical.Software.Spigot.Streams.Azure;
using Archetypical.Software.Spigot.Streams.GoogleCloud;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Spigot.Tests
{
    public class BasicFunctionalityTests
    {
        private ILoggerFactory factory;
        private static ILogger logger;

        public BasicFunctionalityTests(ITestOutputHelper outputHelper)
        {
            factory = new LoggerFactory();
            factory.AddProvider(new XunitLoggerProvider(outputHelper));

            Debug.Listeners.Add(new DefaultTraceListener());
            Archetypical.Software.Spigot.Spigot.Setup(settings =>
            {
                settings.AddLoggerFactory(factory);
            });
            logger = factory.CreateLogger<BasicFunctionalityTests>();
        }

        [Fact]
        [Category("AWS")]
        public async Task AWS_Kinesis_Basic_Test()
        {
            var credentials = new BasicAWSCredentials("AKIAJKNIPBSOX5OZH25Q", "BlSamyco2/nMjLf/nXdc6W8SuWB1Q+cFjGc1Rd4c");
            using (var kinesisStream = await KinesisStream.BuildAsync(builder =>
            {
                builder.ClientConfig = new AmazonKinesisConfig
                {
                    RegionEndpoint = RegionEndpoint.USEast1,
                    MaxErrorRetry = 10,
                };
                builder.Credentials = credentials;
                builder.StreamName = "test";
            }))
            {
                TestStream(kinesisStream);
            }
        }

        [Fact]
        [Category("AWS")]
        public async Task AWS_Sns_Basic_Test()
        {
            var credentials = new BasicAWSCredentials("AKIAJKNIPBSOX5OZH25Q", "BlSamyco2/nMjLf/nXdc6W8SuWB1Q+cFjGc1Rd4c");
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
        [Category("Azure")]
        public async Task Azure_ServiceBus_Basic_Test()
        {
            using (var azureStream = await AzureServiceBusStream.BuildAsync(builder =>
             {
                 builder.ConnectionStringBuilder = new ServiceBusConnectionStringBuilder
                 {
                     EntityPath = "spigottopicname",
                     SasKeyName = "Spigot",
                     SasKey = "Z8fyvzywnE3V407/n1CtQPsCPtqz5KJgit7PsiFQeIE=",
                     TransportType = TransportType.Amqp,
                     Endpoint = "https://spigot.servicebus.windows.net"
                 };
                 builder.TopicName = "spigottopicname";
                 builder.SubscriptionName = "spigot";
             }))
            {
                TestStream(azureStream);
            }
        }

        [Fact]
        [Category("Google Cloud")]
        public async Task Google_Cloud_Basic_Test()
        {
            logger.Log(LogLevel.Information,
                "Google cloud can use a config json stored in an env variable GOOGLE_APPLICATION_CREDENTIALS. That value is {0}",
                Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"));
            using (var googleCloud = await GoogleCloudPubSubStream.BuildAsync(settings =>
                {
                    settings.ProjectId = "crypto-lodge-222219";
                }))
            {
                {
                    TestStream(googleCloud);
                }
            }
        }

        [Fact]
        [Category("Rabbit MQ")]
        public async Task RabbitMQ_PubSub_Basic_Test()
        {
            using (var rabbitMq =
                await Archetypical.Software.Spigot.Streams.RabbitMq.RabbitMqStream.BuildAsync(settings => { }))
            {
                TestStream(rabbitMq);
            }
        }

        [Fact]
        [Category("Redis")]
        public async Task Redis_PubSub_Basic_Test()
        {
            using (var redis = await Archetypical.Software.Spigot.Streams.Redis.Stream.BuildAsync(settings => { }))
            {
                TestStream(redis);
            }
        }

        [Fact]
        [Category("Kafka")]
        public async Task Kafka_PubSub_Basic_Test()
        {
            using (var kafka = Archetypical.Software.Spigot.Streams.Kafka.KafkaStream.Build(settings => { }))
            {
                TestStream(kafka);
            }
        }

        [Fact]
        [Category("ZeroMQ")]
        public async Task ZeroMQ_PubSub_Basic_Test()
        {
            using (XpubXsubIntermediary.Start())
            {
                await Task.Delay(500);
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

        private static void TestStream(ISpigotStream stream)
        {
            Assert.NotNull(stream);
            var expected = Guid.NewGuid();
            var dataToSend = expected.ToByteArray();
            Guid actual = Guid.Empty;
            var signal = new AutoResetEvent(false);
            stream.DataArrived += (sender, bytes) =>
            {
                if (new Guid(bytes) == expected)
                {
                    actual = new Guid(bytes);
                    signal.Set();
                }
            };
            Thread.Sleep(100);
            var sw = Stopwatch.StartNew();
            Assert.True(stream.TrySend(dataToSend));
            signal.WaitOne(TimeSpan.FromSeconds(2));
            sw.Stop();
            logger.Log(LogLevel.Information, "Roundtrip by {1} in {0}", sw.Elapsed, stream.GetType().FullName);
            Assert.Equal(expected, actual);
        }
    }

    public class XpubXsubIntermediary : IDisposable
    {
        private XPublisherSocket xpubSocket;
        private XSubscriberSocket xsubSocket;
        private Proxy proxy;
        private Task listenter;

        public static XpubXsubIntermediary Start()
        {
            var instance = new XpubXsubIntermediary();
            instance.Init();
            return instance;
        }

        private void Init()
        {
            xpubSocket = new XPublisherSocket("@tcp://127.0.0.1:1234");
            xsubSocket = new XSubscriberSocket("@tcp://127.0.0.1:5677");
            // proxy messages between frontend / backend
            proxy = new Proxy(xsubSocket, xpubSocket);
            // blocks indefinitely
            listenter = Task.Factory.StartNew(() => proxy.Start());
        }

        private XpubXsubIntermediary()
        {
        }

        public void Dispose()
        {
            proxy.Stop();
            xpubSocket.Dispose();
            xsubSocket.Dispose();
        }
    }
}