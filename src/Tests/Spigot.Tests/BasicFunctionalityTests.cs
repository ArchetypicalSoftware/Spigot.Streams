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
                await Archetypical.Software.Spigot.Streams.RabbitMq.RabbitMqStream.BuildAsync(settings =>
                {
                    settings.ConnectionFactory = new RabbitMQ.Client.ConnectionFactory
                    {
                        HostName = "barnacle.rmq.cloudamqp.com",
                        UserName = "lgtephse",
                        Password = "IGTvZ0MkS2bxBFY4bODO_LyWDODMF3yZ",
                        VirtualHost = "lgtephse"
                    };
                }))
            {
                TestStream(rabbitMq);
            }
        }

        [Fact]
        [Category("Rabbit MQ")]
        public async Task RabbitMQ_PubSub_MultiStream_Test()
        {
            using (var rabbitMq1 =
                await Archetypical.Software.Spigot.Streams.RabbitMq.RabbitMqStream.BuildAsync(settings =>
                {
                    settings.ConnectionFactory = new RabbitMQ.Client.ConnectionFactory
                    {
                        HostName = "barnacle.rmq.cloudamqp.com",
                        UserName = "lgtephse",
                        Password = "IGTvZ0MkS2bxBFY4bODO_LyWDODMF3yZ",
                        VirtualHost = "lgtephse"
                    };
                })) using (var rabbitMq2 =
                await Archetypical.Software.Spigot.Streams.RabbitMq.RabbitMqStream.BuildAsync(settings =>
                {
                    settings.ConnectionFactory = new RabbitMQ.Client.ConnectionFactory
                    {
                        HostName = "barnacle.rmq.cloudamqp.com",
                        UserName = "lgtephse",
                        Password = "IGTvZ0MkS2bxBFY4bODO_LyWDODMF3yZ",
                        VirtualHost = "lgtephse"
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
            using (var redis = await Archetypical.Software.Spigot.Streams.Redis.Stream.BuildAsync(settings =>
                {
                    settings.ConfigurationOptions.EndPoints.Add(
                        "redis-13640.c10.us-east-1-2.ec2.cloud.redislabs.com:13640");
                    settings.ConfigurationOptions.Password = "VUeuFKLOqWqvpvRfCnKG0a2ACXbIEiXK";
                }))
            {
                TestStream(redis);
            }
        }

        [Fact]
        [Category("Redis")]
        public async Task Redis_PubSub_MultipleStream_Test()
        {
            using (var redis1 = await Archetypical.Software.Spigot.Streams.Redis.Stream.BuildAsync(settings =>
            {
                settings.ConfigurationOptions.EndPoints.Add(
                    "redis-13640.c10.us-east-1-2.ec2.cloud.redislabs.com:13640");
                settings.ConfigurationOptions.Password = "VUeuFKLOqWqvpvRfCnKG0a2ACXbIEiXK";
            })) using (var redis2 = await Archetypical.Software.Spigot.Streams.Redis.Stream.BuildAsync(settings =>
            {
                settings.ConfigurationOptions.EndPoints.Add(
                    "redis-13640.c10.us-east-1-2.ec2.cloud.redislabs.com:13640");
                settings.ConfigurationOptions.Password = "VUeuFKLOqWqvpvRfCnKG0a2ACXbIEiXK";
            }))
            {
                TestMultipleInstancesOfStream(redis1, redis2);
            }
        }

        [Fact]
        [Category("Kafka")]
        public async Task Kafka_PubSub_Basic_Test()
        {
            using (var kafka = await Archetypical.Software.Spigot.Streams.Kafka.KafkaStream.BuildAsync(settings =>
            {
                var brokers = "velomobile-01.srvs.cloudkafka.com:9094,velomobile-03.srvs.cloudkafka.com:9094,velomobile-02.srvs.cloudkafka.com:9094";
                //settings.Topic.Name = "wjbna946-spigot";
                settings.Topic.Name = "wjbna946-default";
                //settings.ProducerConfig.BatchNumMessages = 1000;
                //settings.ProducerConfig.QueueBufferingMaxMessages = 100;
                //settings.ProducerConfig.MessageTimeoutMs = 100;
                //settings.ProducerConfig.Acks = 1;
                //settings.ProducerConfig.SessionTimeoutMs = 6000;
                //settings.ProducerConfig.HeartbeatIntervalMs = 60000;

                settings.ProducerConfig.BootstrapServers = brokers;
                settings.ProducerConfig.SaslUsername = "wjbna946";
                settings.ProducerConfig.SaslPassword = "szI-QN7RfwqKFaSt-p1AaBJRAGFMIMcx";

                settings.ProducerConfig.SaslMechanism = Confluent.Kafka.SaslMechanismType.ScramSha256;
                settings.ProducerConfig.SecurityProtocol = Confluent.Kafka.SecurityProtocolType.Sasl_Ssl;
                //var cert = new FileInfo("karafka.crt");
                //if (cert.Exists)
                //{
                //    settings.ProducerConfig.SslCertificateLocation = cert.FullName;
                //}

                settings.ProducerConfig.Debug = "generic,broker,topic,metadata,feature,queue,protocol,msg,security,all";

                settings.ConsumerConfig = new Confluent.Kafka.ConsumerConfig(settings.ProducerConfig);
                settings.ConsumerConfig.GroupId = "wjbna946-Spigot";
            }))
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
                using (var netmq = await Archetypical.Software.Spigot.Streams.ZeroMQ.Stream.BuildAsync(settings =>
                {
                    settings.XPublisherSocketConnectionString = @"tcp://127.0.0.1:1234";
                    settings.XSubscriberSocketConnectionString = @"tcp://127.0.0.1:5677";
                }))
                {
                    logger.LogInformation($"Sending is successful: {netmq.TrySend(Guid.NewGuid().ToByteArray())}"); ;
                    TestStream(netmq);
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
            signal.WaitOne(TimeSpan.FromSeconds(2));
            sw.Stop();
            logger.Log(LogLevel.Information, "Roundtrip by {1} in {0}", sw.Elapsed, stream.GetType().FullName);
            Assert.Equal(expected, actual);
        }

        private static void TestMultipleInstancesOfStream(ISpigotStream streamInstance1, ISpigotStream streamInstance2)
        {
            Assert.NotNull(streamInstance1);
            Assert.NotNull(streamInstance2);
            var expected1 = Guid.NewGuid();
            var expected2 = Guid.NewGuid();
            var dataToSend1 = expected1.ToByteArray();
            var dataToSend2 = expected2.ToByteArray();
            Guid actual1 = Guid.Empty;
            Guid actual2 = Guid.Empty;
            var signal = new AutoResetEvent(false);

            void OnStreamInstance1OnDataArrived(object sender, byte[] bytes)
            {
                if (new Guid(bytes) != expected1)
                {
                    return;
                }

                actual1 = new Guid(bytes);
                signal.Set();
            }
            void OnStreamInstance2OnDataArrived(object sender, byte[] bytes)
            {
                if (new Guid(bytes) != expected2)
                {
                    return;
                }

                actual2 = new Guid(bytes);
                signal.Set();
            }

            streamInstance1.DataArrived += OnStreamInstance1OnDataArrived;

            Thread.Sleep(100);
            var sw = Stopwatch.StartNew();
            Assert.True(streamInstance2.TrySend(dataToSend1));
            signal.WaitOne(TimeSpan.FromSeconds(2));
            sw.Stop();
            logger.Log(LogLevel.Information, "Roundtrip by {1} in {0}", sw.Elapsed, streamInstance1.GetType().FullName);
            Assert.Equal(expected1, actual1);
            streamInstance1.DataArrived -= OnStreamInstance1OnDataArrived;
            streamInstance2.DataArrived += OnStreamInstance2OnDataArrived;
            signal.Reset();
            Thread.Sleep(100);
            sw = Stopwatch.StartNew();
            Assert.True(streamInstance1.TrySend(dataToSend2));
            signal.WaitOne(TimeSpan.FromSeconds(2));
            sw.Stop();
            logger.Log(LogLevel.Information, "Roundtrip by {1} in {0}", sw.Elapsed, streamInstance2.GetType().FullName);
            Assert.Equal(expected2, actual2);
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
            Thread.Sleep(1000); // time to start up
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