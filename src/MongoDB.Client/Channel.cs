﻿using MongoDB.Client.Bson.Document;
using MongoDB.Client.Bson.Serialization;
using MongoDB.Client.Messages;
using MongoDB.Client.Network;
using MongoDB.Client.Protocol.Common;
using MongoDB.Client.Protocol.Core;
using MongoDB.Client.Protocol.Readers;
using MongoDB.Client.Protocol.Writers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;

namespace MongoDB.Client
{
    internal class Channel : IAsyncDisposable
    {
        private readonly EndPoint _endpoint;
        private readonly NetworkConnectionFactory _connectionFactory;
        private Connection? _connection;
        private ProtocolReader? _reader;
        private ProtocolWriter? _writer;
        private static readonly MessageHeaderReader messageHeaderReader = new MessageHeaderReader();
        private static readonly ReplyMessageReader replyMessageReader = new ReplyMessageReader();

        private static readonly ReadOnlyMemoryWriter memoryWriter = new ReadOnlyMemoryWriter();


        private TaskCompletionSource<MongoMessage> completionSource = new TaskCompletionSource<MongoMessage>();
        private CancellationTokenSource _shutdownToken = new CancellationTokenSource();
        private Task? _readingTask;

        private static readonly Dictionary<Type, IBsonSerializable> _serializerMap = new Dictionary<Type, IBsonSerializable>
        {
            [typeof(BsonDocument)] = new BsonDocumentSerializer(),
            [typeof(MongoDBConnectionInfo)] = new MongoDB.Client.Bson.Serialization.Generated.MongoDBConnectionInfoGeneratedSerializator()
        };

        public Channel(EndPoint endpoint)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _connectionFactory = new NetworkConnectionFactory();
        }

        internal async Task ConnectAsync(CancellationToken cancellationToken)
        {
            _connection = await _connectionFactory.ConnectAsync(_endpoint, null, cancellationToken).ConfigureAwait(false);
            if (_connection is null)
            {
                ThrowHelper.ConnectionException(_endpoint);
            }

            _reader = new ProtocolReader(_connection.Pipe.Input);
            _writer = new ProtocolWriter(_connection.Pipe.Output);
            _readingTask = StartReadAsync();
        }
        

        private async Task StartReadAsync()
        {
            if (_reader is null)
            {
                ThrowHelper.ConnectionException(_endpoint);
            }
            MongoMessage message;
            while (_shutdownToken.IsCancellationRequested == false)
            {
                var headerResult = await _reader.ReadAsync(messageHeaderReader, _shutdownToken.Token).ConfigureAwait(false);
                _reader.Advance();
                switch (headerResult.Message.Opcode)
                {
                    case Opcode.Reply:
                        var replyResult = await _reader.ReadAsync(replyMessageReader, _shutdownToken.Token).ConfigureAwait(false);
                        _reader.Advance();
                        message = new ReplyMessage(headerResult.Message, replyResult.Message);
                        completionSource.TrySetResult(message);
                        break;
                    case Opcode.Message:
                    case Opcode.Update:
                    case Opcode.Insert:
                    case Opcode.Query:
                    case Opcode.GetMore:
                    case Opcode.Delete:
                    case Opcode.KillCursors:
                    case Opcode.Compressed:
                    case Opcode.OpMsg:
                    default:
                        ThrowHelper.OpcodeNotSupportedException(headerResult.Message.Opcode); //TODO: need to read pipe to end
                        break;
                }
            }
        }

        public async ValueTask<TResp> SendAsync<TResp>(ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
        {
            if (_shutdownToken.IsCancellationRequested == false)
            {
                if (_writer is not null)
                {
                    completionSource = new TaskCompletionSource<MongoMessage>();

                    await _writer.WriteAsync(memoryWriter, message, cancellationToken).ConfigureAwait(false);

                    var response = await completionSource.Task.ConfigureAwait(false);
                    return await ParseAsync<TResp>(response).ConfigureAwait(false);
                }

                ThrowHelper.ConnectionException(_endpoint);
                return default;
            }
            ThrowHelper.ObjectDisposedException(nameof(Channel));
            return default;


            // Temp implementation
            async ValueTask<T> ParseAsync<T>(MongoMessage message)
            {
                switch (message)
                {
                    case ReplyMessage replyMessage:
                        if (_serializerMap.TryGetValue(typeof(T), out var serializer))
                        {
                            var bodyReader = new BodyReader(serializer);
                            var bodyResult = await _reader!.ReadAsync(bodyReader, _shutdownToken.Token).ConfigureAwait(false);
                            _reader.Advance();
                            return (T)bodyResult.Message;
                        }

                        ThrowHelper.UnsupportedTypeException(typeof(T));
                        return default;
                    default:
                        ThrowHelper.UnsupportedTypeException(typeof(T));
                        return default;
                }
            }
        }

        private BsonDocument GetHelloMessage()
        {
            var root = new BsonDocument();
            var driverDoc = new BsonDocument();

            driverDoc.Elements.AddRange(new List<BsonElement>{
                BsonElement.Create(driverDoc, "driver", "MongoDB.Client"),
                BsonElement.Create(driverDoc, "version", "0.0.0"),
            });
            root.Elements.AddRange(new List<BsonElement>
            {
                BsonElement.Create(root, "driver", driverDoc)
            });

            return root;
        }

        public async ValueTask DisposeAsync()
        {
            _shutdownToken.Cancel();
            if (_readingTask is not null)
            {
                await _readingTask.ConfigureAwait(false);
            }
            if (_connection is not null)
            {
                await _connection.CloseAsync().ConfigureAwait(false);
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
