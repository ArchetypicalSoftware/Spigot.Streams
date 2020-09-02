using Archetypical.Software.Spigot.Streams.Azure;
using Archetypical.Software.Spigot.Streams.GoogleCloud;
using Archetypical.Software.Spigot.Streams.RabbitMq;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Archetypical.Software.Spigot.Extensions;
using Archetypical.Software.Spigot.Streams.ZeroMQ;
using Xunit;
using Xunit.Abstractions;
using Stream = Archetypical.Software.Spigot.Streams.Redis.RedisStream;

namespace Spigot.Tests
{
    public class BasicFunctionalityTests : SpigotSerializerTests
    {
        public BasicFunctionalityTests(ITestOutputHelper helper) : base(helper)
        {
        }

        //[Fact]
        //[Category("Azure")]
        //public async Task Azure_ServiceBus_Basic_Test()
        //{
        //    var block = GetBuilder();
        //    block.AddAzureServiceBus(builder =>
        //    {
        //        var section = block.Configuration.GetSection("Azure");
        //        builder.Logger = factory.CreateLogger<AzureServiceBusStream>();
        //        builder.TopicName = section.GetValue<string>("TopicName");
        //        builder.ConnectionStringBuilder = new ServiceBusConnectionStringBuilder
        //        {
        //            SasKeyName = section.GetValue<string>("SasKeyName"),
        //            SasKey = section.GetValue<string>("SasKey"),
        //            TransportType = TransportType.Amqp,
        //            Endpoint = section.GetValue<string>("Endpoint")
        //        };
        //    });
        //    block.Build();
        //    using (var azureStream = block.Stream)

        //        TestStream(azureStream);
        //}

        //[Fact]
        //[Category("Azure")]
        //public async Task Azure_ServiceBus_Burst_Test()
        //{
        //    var block = GetBuilder();
        //    block.AddAzureServiceBus(builder =>
        //    {
        //        var section = block.Configuration.GetSection("Azure");
        //        builder.Logger = factory.CreateLogger<AzureServiceBusStream>();
        //        builder.TopicName = section.GetValue<string>("TopicName");
        //        builder.ConnectionStringBuilder = new ServiceBusConnectionStringBuilder
        //        {
        //            SasKeyName = section.GetValue<string>("SasKeyName"),
        //            SasKey = section.GetValue<string>("SasKey"),
        //            TransportType = TransportType.Amqp,
        //            Endpoint = section.GetValue<string>("Endpoint")
        //        };
        //    });
        //    block.Build();
        //    using (var azureStream = block.Stream)
        //        TestBurstStream(azureStream);
        //}

        //[Fact]
        //[Category("Azure")]
        //public async Task Azure_ServiceBus_Multiple_Test()
        //{
        //    var block1 = GetBuilder();
        //    block1.AddAzureServiceBus(builder =>
        //    {
        //        var section = block1.Configuration.GetSection("Azure");
        //        builder.Logger = factory.CreateLogger<AzureServiceBusStream>();
        //        builder.TopicName = section.GetValue<string>("TopicName");
        //        builder.ConnectionStringBuilder = new ServiceBusConnectionStringBuilder
        //        {
        //            SasKeyName = section.GetValue<string>("SasKeyName"),
        //            SasKey = section.GetValue<string>("SasKey"),
        //            TransportType = TransportType.Amqp,
        //            Endpoint = section.GetValue<string>("Endpoint")
        //        };
        //    });
        //    block1.Build();

        //    var block2 = GetBuilder();
        //    block2.AddAzureServiceBus(builder =>
        //    {
        //        var section = block2.Configuration.GetSection("Azure");
        //        builder.Logger = factory.CreateLogger<AzureServiceBusStream>();
        //        builder.TopicName = section.GetValue<string>("TopicName");
        //        builder.ConnectionStringBuilder = new ServiceBusConnectionStringBuilder
        //        {
        //            SasKeyName = section.GetValue<string>("SasKeyName"),
        //            SasKey = section.GetValue<string>("SasKey"),
        //            TransportType = TransportType.Amqp,
        //            Endpoint = section.GetValue<string>("Endpoint")
        //        };
        //    });
        //    block2.Build();
        //    using (var azureStream1 = block1.Stream)
        //    using (var azureStream2 = block2.Stream)

        //        TestMultipleInstancesOfStream(azureStream1, azureStream2);
        //}

        private ZeroMqStream GetZeroMqStream()
        {
            var stream = new ZeroMqStream(factory.CreateLogger<ZeroMqStream>());
            var settings = new ZeroMqSettings();
            settings.XPublisherSocketConnectionString = @"tcp://127.0.0.1:1234";
            settings.XSubscriberSocketConnectionString = @"tcp://127.0.0.1:5677";
            stream.Init(settings);
            return stream;
        }

        [Fact]
        [Category("ZeroMQ")]
        public async Task ZeroMQ_PubSub_Basic_Test()
        {
            using (XpubXsubIntermediary.Start())
            {
                using (var netmq = GetZeroMqStream())
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
                using (var netmq = GetZeroMqStream())
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
                using (var netmq1 = GetZeroMqStream())
                using (var netmq2 = GetZeroMqStream())
                {
                    TestMultipleInstancesOfStream(netmq1, netmq2);
                }
            }
        }
    }
}