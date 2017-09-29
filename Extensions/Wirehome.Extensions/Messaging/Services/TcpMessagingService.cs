﻿using Wirehome.Contracts.Logging;
using Wirehome.Contracts.Messaging;
using Wirehome.Extensions.Contracts;
using Wirehome.Extensions.Messaging.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Wirehome.Extensions.Messaging.Services
{
    public class TcpMessagingService : ITcpMessagingService
    {
        private readonly ILogger _logService;
        private readonly IMessageBrokerService _messageBroker;
        private readonly List<IBinaryMessage> _messageHandlers = new List<IBinaryMessage>();

        public TcpMessagingService(ILogService logService, IMessageBrokerService messageBroker, IEnumerable<IBinaryMessage> handlers)
        {
            _logService = logService.CreatePublisher(nameof(TcpMessagingService));
            _messageBroker = messageBroker;
            _messageHandlers.AddRange(handlers);
        }

        public Task Initialize()
        {
            _messageHandlers.ForEach(handler =>
            {
                _messageBroker.Subscribe(new MessageSubscription
                {
                    Id = Guid.NewGuid().ToString(),
                    PayloadType = handler.GetType().Name,
                    Topic = typeof(TcpMessagingService).Name,
                    Callback = MessageHandler
                });
            });

            return Task.CompletedTask;
        }

        private async void MessageHandler(Message<JObject> message)
        {
            var tasks = _messageHandlers.Select(i => SendMessage(message, i));
            await Task.WhenAll(tasks);
        }

        private async Task SendMessage(Message<JObject> message, IBinaryMessage handler)
        {
            if (handler.CanSerialize(message.Payload.Type))
            {
                try
                {
                    var tcpMessage = message.Payload.Content.ToObject<IBaseMessage>();

                    //TODO
                    //using(var socket = new TcpSocketClient())
                    //{
                    //    Uri uri = new Uri($"tcp://{tcpMessage.Address}");

                    //    await socket.ConnectAsync(uri.Host, uri.Port.ToString());
                    //    var messageBytes = handler.Serialize(message.Payload.Content);
                    //    await socket.WriteStream.WriteAsync(messageBytes, 0, messageBytes.Length);
                    //    //TODO CHECK
                    //    await socket.WriteStream.FlushAsync();
                        
                    //    var reader = new StreamReader(socket.ReadStream);
                    //    string response = await reader.ReadLineAsync();

                    //    socket.Disconnect();
                    //}
                }
                catch (Exception ex)
                {
                    _logService.Error(ex, $"Handler of type {handler.GetType().Name} failed to process message");
                }
            }
        }
    }

   
}
