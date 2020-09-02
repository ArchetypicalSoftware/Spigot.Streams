using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Archetypical.Software.Spigot;
using Archetypical.Software.Spigot.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Spigot.Tests
{
    public abstract class SpigotSerializerTests
    {
        private readonly ITestOutputHelper outputHelper;

        protected SpigotSerializerTests(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper;

            factory = new LoggerFactory();
            factory.AddProvider(new XunitLoggerProvider(outputHelper));
            logger = factory.CreateLogger<BasicFunctionalityTests>();

            config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json", true, false)
                .AddEnvironmentVariables("SPIGOT_")
                //In environment variables, a colon separator may not work on all platforms. A double underscore (__) is supported by all platforms and is converted to a colon.
                .Build();
        }

        protected readonly ILoggerFactory factory;
        protected static ILogger logger;
        protected readonly IConfigurationRoot config;

        protected static void TestStream(ISpigotStream stream)
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

        protected static void TestBurstStream(ISpigotStream stream, int messagesToSend = 10)
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

        protected static void TestMultipleInstancesOfStream(ISpigotStream streamInstance1, ISpigotStream streamInstance2)
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
    }
}