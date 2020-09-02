using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SimpleNotificationService.Util;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Archetypical.Software.Spigot.Streams.AWS
{
    public class SnsStream : ISpigotStream, IDisposable
    {
        private readonly ILogger<SnsStream> logger;
        private static readonly BlockingCollection<Tuple<HttpListenerRequest, Message>> Requests = new BlockingCollection<Tuple<HttpListenerRequest, Message>>();
        private CancellationTokenSource _cancellationTokenSource;
        private AmazonSimpleNotificationServiceClient _client;
        private HttpListener _httpListener;
        private Task _listentingTask;
        private Task _processingTask;
        private SubscribeResponse _subscription;
        private Topic _topic;

        internal SnsStream(ILogger<SnsStream> logger)
        {
            this.logger = logger;
        }

        ~SnsStream()
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
            var result = _client.PublishAsync(new PublishRequest
            {
                Message = Convert.ToBase64String(data),
                TopicArn = _topic.TopicArn
            }).GetAwaiter().GetResult();
            return !string.IsNullOrWhiteSpace(result.MessageId);
        }

        private async Task<ConfirmSubscriptionResponse> ConfirmSubscriptionAsync(Message confirmationRequest)
        {
            string token = confirmationRequest.Token;
            string topicArn = confirmationRequest.TopicArn;
            return await _client.ConfirmSubscriptionAsync(new ConfirmSubscriptionRequest
            {
                Token = token,
                TopicArn = topicArn
            });
        }

        internal async Task InitAsync(SnsStreamSettings settings)
        {
            _cancellationTokenSource = new CancellationTokenSource();

            _client = new AmazonSimpleNotificationServiceClient(settings.AwsCredentials, settings.AmazonSimpleNotificationServiceConfig);
            await _client.ListTopicsAsync();

            _topic = await _client.FindTopicAsync(settings.TopicName);

            if (_topic == null)
            {
                var createTopicResponse = await _client.CreateTopicAsync(new CreateTopicRequest
                {
                    Name = settings.TopicName
                });
                if (createTopicResponse.HttpStatusCode == System.Net.HttpStatusCode.OK)
                    _topic = new Topic
                    {
                        TopicArn = createTopicResponse.TopicArn
                    };
            }

            if (_topic == null)
                throw new WebException("Failed to create or connect to a topic");

            _httpListener = new HttpListener
            {
                AuthenticationSchemes = AuthenticationSchemes.Anonymous
            };
            _httpListener.Prefixes.Add(settings.CallbackSettings.Prefix);
            _httpListener.Start();

            _listentingTask = StartListening();
            _processingTask = Task.Run(() => StartProcessing());

            _subscription = await _client.SubscribeAsync(new SubscribeRequest
            {
                TopicArn = _topic.TopicArn,
                Protocol = settings.CallbackSettings.Protocol == Protocol.Http ? "http" : "https",
                Endpoint = settings.CallbackSettings.Prefix,
            });
        }

        private async Task ProcessNotification(Message message)
        {
            if (DataArrived == null) return;
            await Task.Factory.FromAsync(
                DataArrived.BeginInvoke(
                    this,
                    Convert.FromBase64String(message.MessageText),
                    DataArrived.EndInvoke,
                    null),
                DataArrived.EndInvoke,
                TaskCreationOptions.None);
        }

        private void ReleaseUnmanagedResources()
        {
            UnSubscribeAsync().GetAwaiter().GetResult();
            // TODO unsubscribe and turn off the listener
            _cancellationTokenSource.Cancel();
            _httpListener?.Stop();
        }

        private async Task StartListening()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                var context = await _httpListener.GetContextAsync();
                var request = context.Request;
                using (var stream = new StreamReader(request.InputStream))
                {
                    var body = await stream.ReadToEndAsync();
                    var message = Message.ParseMessage(body);
                    if (message.IsMessageSignatureValid()) // Only accept valid messages
                        Requests.Add(new Tuple<HttpListenerRequest, Message>(request, message));
                }
                //Return a valid response indicating we received the message successfully
                context.Response.StatusCode = 200;
                context.Response.ContentLength64 = 0;
                context.Response.OutputStream.Close();
            }
        }

        private void StartProcessing()
        {
            foreach (var request in Requests.GetConsumingEnumerable(_cancellationTokenSource.Token))
            {
                if (request.Item2.IsSubscriptionType)
                {
                    var response = ConfirmSubscriptionAsync(request.Item2).GetAwaiter().GetResult();
                    if (response.HttpStatusCode == HttpStatusCode.OK)
                        _subscription.SubscriptionArn = response.SubscriptionArn;
                    continue;
                }

                if (request.Item2.IsNotificationType)
                {
                    Task.Run(() => ProcessNotification(request.Item2));

                    continue;
                }

                if (request.Item2.IsUnsubscriptionType)
                    continue;
            }
        }

        private async Task<UnsubscribeResponse> UnSubscribeAsync()
        {
            if (_subscription == null || _subscription.SubscriptionArn != "pending confirmation") return null;
            return await _client.UnsubscribeAsync(new UnsubscribeRequest
            {
                SubscriptionArn = _subscription.SubscriptionArn
            });
        }
    }
}