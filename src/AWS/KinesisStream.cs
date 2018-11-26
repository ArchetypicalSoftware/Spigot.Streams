using Amazon.Kinesis;
using Amazon.Kinesis.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Archetypical.Software.Spigot.Streams.AWS
{
    public class KinesisStream : ISpigotStream, IDisposable
    {
        private const string PartionKey = "Spigot Stream";
        private AmazonKinesisClient _client;
        private CancellationTokenSource _cancellationTokenSource;
        private KinesisSettings _settings;
        private CreateStreamResponse _deliveryStream;
        private Task _listeningTask;
        private ILogger<KinesisStream> Logger;

        public static async Task<KinesisStream> BuildAsync(Action<KinesisSettings> builder)
        {
            var settings = new KinesisSettings();
            builder(settings);
            var instance = new KinesisStream();
            await instance.Init(settings);
            return instance;
        }

        private async Task StartListening(List<Shard> shards)
        {
            await Task.WhenAll(shards.Select(ListenToShard));
        }

        private async Task ListenToShard(Shard shard)
        {
            Logger?.LogDebug("Preparing to listen to Shard {0}", shard.ShardId);
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
                Logger?.LogTrace("Got iterator {0} for shard {1}", iteratorId, shard.ShardId);
                while (!_cancellationTokenSource.IsCancellationRequested && !string.IsNullOrEmpty(iteratorId))
                {
                    var getRequest = new GetRecordsRequest { Limit = 1000, ShardIterator = iteratorId };

                    var getResponse = await _client.GetRecordsAsync(getRequest);
                    var nextIterator = getResponse.NextShardIterator;
                    var records = getResponse.Records;

                    if (records.Count > 0)
                    {
                        Logger?.LogTrace("Got {0} records from shard {1}", records.Count, shard.ShardId);
                        foreach (var record in records)
                        {
                            if (DataArrived != null)
                            {
                                await Task.Factory.FromAsync(
                                    DataArrived.BeginInvoke(this, record.Data.ToArray(), DataArrived.EndInvoke,
                                        null), DataArrived.EndInvoke, TaskCreationOptions.None);
                            }
                        }
                    }
                    iteratorId = nextIterator;
                }
            }
        }

        private async Task Init(KinesisSettings settings)
        {
            _settings = settings;
            Logger = settings.Logger;
            Logger?.LogInformation("Building a new Kinesis stream");
            _cancellationTokenSource = new CancellationTokenSource();

            _client = new AmazonKinesisClient(settings.Credentials, settings.ClientConfig);

            var streams = await _client.ListShardsAsync(new ListShardsRequest
            {
                StreamName = settings.StreamName
            });
            if (streams.Shards != null && streams.Shards.Any())
            {
                Logger?.LogDebug("Found {0} previously created shards on stream {1}", streams.Shards.Count, settings.StreamName);
                _listeningTask = StartListening(streams.Shards);
                return;
            }

            _deliveryStream = await _client.CreateStreamAsync(new CreateStreamRequest
            {
                ShardCount = settings.ShardCount,
                StreamName = settings.StreamName
            });
            Logger?.LogDebug("Successfully created stream {0} with {1} shard", settings.StreamName,
                settings.ShardCount);
            var stream = await _client.DescribeStreamAsync(new DescribeStreamRequest
            {
                StreamName = settings.StreamName
            });

            _listeningTask = StartListening(stream.StreamDescription.Shards);
        }

        private KinesisStream()
        {
        }

        public bool TrySend(byte[] data)
        {
            var putRecordRequest = new PutRecordRequest();
            Logger?.LogTrace($"Attempting to send {data.Length} bytes to {_settings.StreamName} [partition:{PartionKey}]");
            using (var mem = new MemoryStream(data))
            {
                putRecordRequest.Data = mem;
                putRecordRequest.StreamName = _settings.StreamName;
                putRecordRequest.PartitionKey = PartionKey;
                // Put record into the DeliveryStream
                var putResponse = _client.PutRecordAsync(putRecordRequest).GetAwaiter().GetResult();
                Logger?.LogTrace("Returned {0}", putResponse.HttpStatusCode);
                return putResponse.HttpStatusCode == HttpStatusCode.OK;
            }
        }

        public event EventHandler<byte[]> DataArrived;

        private void ReleaseUnmanagedResources()
        {
            // TODO unsubscribe and turn off the listener
            _cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~KinesisStream()
        {
            ReleaseUnmanagedResources();
        }
    }
}