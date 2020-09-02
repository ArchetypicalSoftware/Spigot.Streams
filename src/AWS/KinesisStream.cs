using Amazon.Kinesis;
using Amazon.Kinesis.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Spigot.Tests")]

namespace Archetypical.Software.Spigot.Streams.AWS
{
    public class KinesisStream : ISpigotStream, IDisposable
    {
        private const string PartitionKey = "Spigot Stream";
        private CancellationTokenSource _cancellationTokenSource;
        private AmazonKinesisClient _client;
        private CreateStreamResponse _deliveryStream;
        private Task _listeningTask;
        private KinesisSettings _settings;
        private ILogger<KinesisStream> logger;

        internal KinesisStream(ILogger<KinesisStream> logger)
        {
            this.logger = logger;
        }

        ~KinesisStream()
        {
            ReleaseUnmanagedResources();
        }

        public event EventHandler<byte[]> DataArrived;

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        public bool TrySend(byte[] data)
        {
            var putRecordRequest = new PutRecordRequest();
            logger?.LogTrace($"Attempting to send {data.Length} bytes to {_settings.StreamName} [partition:{PartitionKey}]");
            using (var mem = new MemoryStream(data))
            {
                putRecordRequest.Data = mem;
                putRecordRequest.StreamName = _settings.StreamName;
                putRecordRequest.PartitionKey = PartitionKey;
                // Put record into the DeliveryStream
                var putResponse = _client.PutRecordAsync(putRecordRequest).GetAwaiter().GetResult();
                logger?.LogTrace("Returned {0}", putResponse.HttpStatusCode);
                return putResponse.HttpStatusCode == HttpStatusCode.OK;
            }
        }

        internal async Task InitAsync(KinesisSettings settings)
        {
            _settings = settings;
            logger?.LogInformation("Building a new Kinesis stream");
            _cancellationTokenSource = new CancellationTokenSource();

            _client = new AmazonKinesisClient(settings.Credentials, settings.ClientConfig);

            var streams = await _client.ListShardsAsync(new ListShardsRequest
            {
                StreamName = settings.StreamName
            });
            if (streams.Shards != null && streams.Shards.Any())
            {
                logger?.LogDebug("Found {0} previously created shards on stream {1}", streams.Shards.Count, settings.StreamName);
                _listeningTask = StartListening(streams.Shards);
                return;
            }

            _deliveryStream = await _client.CreateStreamAsync(new CreateStreamRequest
            {
                ShardCount = settings.ShardCount,
                StreamName = settings.StreamName
            });
            logger?.LogDebug("Successfully created stream {0} with {1} shard", settings.StreamName,
                settings.ShardCount);
            var stream = await _client.DescribeStreamAsync(new DescribeStreamRequest
            {
                StreamName = settings.StreamName
            });

            _listeningTask = StartListening(stream.StreamDescription.Shards);
        }

        private async Task ListenToShard(Shard shard)
        {
            logger?.LogDebug("Preparing to listen to Shard {0}", shard.ShardId);
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                var getIterator = new GetShardIteratorRequest
                {
                    ShardId = shard.ShardId,
                    StreamName = _settings.StreamName,
                    ShardIteratorType = ShardIteratorType.LATEST,
                };
                var iterator = await _client.GetShardIteratorAsync(getIterator);
                var iteratorId = iterator.ShardIterator;
                logger?.LogTrace("Got iterator {0} for shard {1}", iteratorId, shard.ShardId);
                while (!_cancellationTokenSource.IsCancellationRequested && !string.IsNullOrEmpty(iteratorId))
                {
                    var getRequest = new GetRecordsRequest { Limit = 1000, ShardIterator = iteratorId };

                    var getResponse = await _client.GetRecordsAsync(getRequest);
                    var nextIterator = getResponse.NextShardIterator;
                    var records = getResponse.Records;

                    if (records.Count > 0)
                    {
                        logger?.LogTrace("Got {0} records from shard {1}", records.Count, shard.ShardId);
                        foreach (var record in records)
                        {
                            DataArrived?.Invoke(this, record.Data.ToArray());
                        }
                    }
                    iteratorId = nextIterator;
                }
            }
        }

        private void ReleaseUnmanagedResources()
        {
            // TODO unsubscribe and turn off the listener
            _cancellationTokenSource.Cancel();
        }

        private async Task StartListening(List<Shard> shards)
        {
            await Task.WhenAll(shards.Select(ListenToShard));
        }
    }
}