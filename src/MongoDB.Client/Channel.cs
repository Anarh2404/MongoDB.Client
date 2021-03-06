﻿using MongoDB.Client.Bson.Document;
using MongoDB.Client.Bson.Serialization.Generated;
using MongoDB.Client.Messages;
using MongoDB.Client.Network;
using MongoDB.Client.Protocol.Common;
using MongoDB.Client.Protocol.Core;
using MongoDB.Client.Protocol.Readers;
using MongoDB.Client.Protocol.Writers;
using MongoDB.Client.Readers;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Client.Connection;
using MongoDB.Client.Protocol.Messages;

namespace MongoDB.Client
{
    internal class Channel : IAsyncDisposable
    {
        private readonly EndPoint _endpoint;
        private readonly ILogger _logger;
        private readonly NetworkConnectionFactory _connectionFactory;
        private System.Net.Connections.Connection? _connection;
        private readonly BsonDocument _initialDocument;
        private ConnectionInfo? _connectionInfo;
        private ProtocolReader? _reader;
        private ProtocolWriter? _writer;
        private readonly MessageHeaderReader MessageHeaderReader = new MessageHeaderReader();
        private readonly ReplyMessageReader ReplyMessageReader = new ReplyMessageReader();
        private readonly MsgMessageReader MsgMessageReader = new MsgMessageReader();

        // private static readonly ReadOnlyMemoryWriter memoryWriter = new ReadOnlyMemoryWriter();
        private readonly QueryMessageWriter QueryWriter = new QueryMessageWriter();
        private readonly MsgMessageWriter MsgWriter = new MsgMessageWriter();
        private readonly GetMoreMessageWriter GetMoreWriter = new GetMoreMessageWriter();

        // private readonly ConcurrentDictionary<int, TaskCompletionSourceWithCancellation<MongoResponseMessage>>
        //     _completionMap =
        //         new ConcurrentDictionary<int, TaskCompletionSourceWithCancellation<MongoResponseMessage>>();

        private readonly ConcurrentDictionary<int, ParserCompletion>
            _completionMap =
                new ConcurrentDictionary<int, ParserCompletion>();

        // private readonly ManualResetValueTaskSource<MongoResponseMessage> completionSource =
        //     new ManualResetValueTaskSource<MongoResponseMessage>();

        private CancellationTokenSource _shutdownToken = new CancellationTokenSource();
        private Task? _readingTask;
        private readonly SemaphoreSlim _initSemaphore = new SemaphoreSlim(1);
        private Task<ConnectionInfo>? _initTask;
        public bool Init { get; private set; }
        public bool IsBusy => _completionMap.Count > Environment.ProcessorCount;

        private readonly int _channelNum;

        public Channel(EndPoint endpoint, ILoggerFactory loggerFactory, int channelNum)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _channelNum = channelNum;
            _logger = loggerFactory.CreateLogger($"MongoClient: {endpoint}");
            _connectionFactory = new NetworkConnectionFactory();
            _initialDocument = InitHelper.CreateInitialCommand();
        }

        private static readonly byte[] Hell = new byte[]
        {
            59, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 212, 7, 0, 0, 4, 0, 0, 0, 97, 100, 109, 105, 110, 46, 36, 99, 109, 100,
            0, 0, 0, 0, 0, 255, 255, 255, 255, 20, 0, 0, 0, 16, 98, 117, 105, 108, 100, 73, 110, 102, 111, 0, 1, 0, 0,
            0, 0
        };

        private static int _counter;

        public int GetNextRequestNumber()
        {
            var num = Interlocked.Increment(ref _counter);
            return _channelNum * 1000000 + num;
        }

        public ValueTask<ConnectionInfo> InitConnectAsync(CancellationToken cancellationToken)
        {
            if (_connectionInfo is not null)
            {
                return new ValueTask<ConnectionInfo>(_connectionInfo);
            }

            return StartConnectAsync(cancellationToken);

            async ValueTask<ConnectionInfo> StartConnectAsync(CancellationToken ct)
            {
                if (_initTask is null)
                {
                    await _initSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        if (_initTask is null)
                        {
                            _initTask = DoConnectAsync(ct);
                        }
                    }
                    finally
                    {
                        _initSemaphore.Release();
                    }
                }

                return await _initTask.ConfigureAwait(false);
            }

            async Task<ConnectionInfo> DoConnectAsync(CancellationToken ct)
            {
                await ConnectAsync(ct).ConfigureAwait(false);
                QueryMessage? connectRequest = CreateQueryRequest(_initialDocument);
                var configMessage = await SendQueryAsync<BsonDocument>(connectRequest, ct).ConfigureAwait(false);
                QueryMessage? buildInfoRequest = CreateQueryRequest(new BsonDocument("buildInfo", 1));
                var hell = await SendQueryAsync<BsonDocument>(buildInfoRequest, ct).ConfigureAwait(false);
                _connectionInfo = new ConnectionInfo(configMessage[0], hell[0]);
                Init = true;
                return _connectionInfo;
            }
        }

        private async Task ConnectAsync(CancellationToken cancellationToken)
        {
            _connection = await _connectionFactory.ConnectAsync(_endpoint, null, cancellationToken)
                .ConfigureAwait(false);
            if (_connection is null)
            {
                ThrowHelper.ConnectionException<bool>(_endpoint);
            }

            _reader = new ProtocolReader(_connection.Pipe.Input);
            _writer = new ProtocolWriter(_connection.Pipe.Output);
            _readingTask = StartReadAsync();
        }

        private QueryMessage CreateQueryRequest(BsonDocument document)
        {
            var doc = CreateWrapperDocument(document);
            return CreateQueryRequest("admin.$cmd", doc);
        }

        private QueryMessage CreateQueryRequest(string database, BsonDocument document)
        {
            var num = GetNextRequestNumber();
            return new QueryMessage(num, database, document);
        }

        private BsonDocument CreateWrapperDocument(BsonDocument document)
        {
            BsonDocument? readPreferenceDocument = null;
            var doc = new BsonDocument
            {
                {"$query", document},
                {"$readPreference", readPreferenceDocument, readPreferenceDocument != null}
            };

            if (doc.Count == 1)
            {
                return doc["$query"].AsBsonDocument;
            }
            else
            {
                return doc;
            }
        }


        private async Task StartReadAsync()
        {
            if (_reader is null)
            {
                ThrowHelper.ConnectionException<bool>(_endpoint);
            }

            _logger.LogInformation($"Channel {_channelNum} start reading");
            MongoResponseMessage message;
            // TaskCompletionSourceWithCancellation<MongoResponseMessage>? completion;
            ParserCompletion? completion;
            while (_shutdownToken.IsCancellationRequested == false)
            {
                try
                {
                    var headerResult = await _reader.ReadAsync(MessageHeaderReader, _shutdownToken.Token)
                        .ConfigureAwait(false);
                    _reader.Advance();

                    _logger.GotMessage(headerResult.Message.ResponseTo);
                    switch (headerResult.Message.Opcode)
                    {
                        case Opcode.Reply:
                            _logger.GotReplyMessage(headerResult.Message.ResponseTo);
                            var replyResult = await _reader.ReadAsync(ReplyMessageReader, _shutdownToken.Token)
                                .ConfigureAwait(false);
                            _reader.Advance();
                            message = new ReplyMessage(headerResult.Message, replyResult.Message);
                            break;
                        case Opcode.OpMsg:
                            _logger.GotMsgMessage(headerResult.Message.ResponseTo);
                            var msgResult = await _reader.ReadAsync(MsgMessageReader, _shutdownToken.Token)
                                .ConfigureAwait(false);
                            _reader.Advance();
                            message = new ResponseMsgMessage(headerResult.Message, msgResult.Message);
                            break;
                        case Opcode.Message:
                        case Opcode.Update:
                        case Opcode.Insert:
                        case Opcode.Query:
                        case Opcode.GetMore:
                        case Opcode.Delete:
                        case Opcode.KillCursors:
                        case Opcode.Compressed:
                        default:
                            _logger.UnknownOpcodeMessage(headerResult.Message);
                            if (_completionMap.TryGetValue(headerResult.Message.ResponseTo, out completion))
                            {
                                completion.CompletionSource.TrySetException(
                                    new NotSupportedException($"Opcode '{headerResult.Message.Opcode}' not supported"));
                            }

                            continue;
                            //TODO: need to read pipe to end
                            break;
                    }

                    if (_completionMap.TryGetValue(message.Header.ResponseTo, out completion))
                    {
                        var result = await completion.ParseAsync(message).ConfigureAwait(false);
                        completion.CompletionSource.TrySetResult(result);
                    }
                    else
                    {
                        _logger.LogError("Message not found");
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e.ToString());
                }
            }
        }

        public async ValueTask<QueryResult<TResp>> SendQueryAsync<TResp>(QueryMessage message, CancellationToken cancellationToken)
        {
            if (_shutdownToken.IsCancellationRequested == false)
            {
                if (_writer is not null)
                {
                    var completion = _completionMap.GetOrAdd(message.RequestNumber,
                        i => new ParserCompletion(new TaskCompletionSourceWithCancellation<IParserResult>(),
                            response => ParseAsync<TResp>(response)));


                    try
                    {
                        await _writer.WriteAsync(QueryWriter, message, cancellationToken).ConfigureAwait(false);
                        // Debug.WriteLine($"Sent query message '{message.RequestNumber}'");
                        // var response = await new ValueTask<MongoResponseMessage>(completionSource, completionSource.Version)
                        //     .ConfigureAwait(false);
                        // completionSource.Reset();
                        var result = await completion.CompletionSource.WaitWithCancellationAsync(cancellationToken)
                            .ConfigureAwait(false);

                        if (result is QueryResult<TResp> queryResult)
                        {
                            return queryResult;
                        }

                        return default!;
                        // return await ParseAsync<TResp>(response).ConfigureAwait(false);
                    }
                    finally
                    {
                        _completionMap.TryRemove(message.RequestNumber, out _);
                    }
                }

                return ThrowHelper.ConnectionException<QueryResult<TResp>>(_endpoint);
            }

            return ThrowHelper.ObjectDisposedException<QueryResult<TResp>>(nameof(Channel));


            async ValueTask<IParserResult> ParseAsync<T>(MongoResponseMessage mongoResponse)
            {
                var reader = _reader!;
                switch (mongoResponse)
                {
                    case ReplyMessage replyMessage:
                        if (SerializersMap.TryGetSerializer<T>(out var replySerializer))
                        {
                            var bodyReader = new ReplyBodyReader<T>(replySerializer, replyMessage);
                            var bodyResult = await reader.ReadAsync(bodyReader, _shutdownToken.Token)
                                .ConfigureAwait(false);
                            reader.Advance();
                            return bodyReader.Result;
                        }

                        return ThrowHelper.UnsupportedTypeException<QueryResult<T>>(typeof(T));
                    default:
                        return ThrowHelper.UnsupportedTypeException<QueryResult<T>>(typeof(T));
                }
            }
        }

        public async ValueTask<CursorResult<TResp>> GetCursorAsync<TResp>(MsgMessage message,
            CancellationToken cancellationToken)
        {
            if (_shutdownToken.IsCancellationRequested == false)
            {
                if (_writer is not null)
                {
                    // var completion = _completionMap.GetOrAdd(message.RequestNumber,
                    //     i => new TaskCompletionSourceWithCancellation<MongoResponseMessage>());

                    var completion = _completionMap.GetOrAdd(message.RequestNumber,
                        i => new ParserCompletion(new TaskCompletionSourceWithCancellation<IParserResult>(),
                            response => ParseAsync<TResp>(response)));
                    try
                    {
                        await _writer.WriteAsync(MsgWriter, message, cancellationToken).ConfigureAwait(false);
                        _logger.SentCursorMessage(message.RequestNumber);
                        // var response = await new ValueTask<MongoResponseMessage>(completionSource, completionSource.Version)
                        //     .ConfigureAwait(false);
                        // completionSource.Reset();
                        var response = await completion.CompletionSource.WaitWithCancellationAsync(cancellationToken)
                            .ConfigureAwait(false);
                        if (response is CursorResult<TResp> cursor)
                        {
                            return cursor;
                        }

                        return default!;
                        // return await ParseAsync<TResp>(response, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        _completionMap.TryRemove(message.RequestNumber, out _);
                    }
                }

                return ThrowHelper.ConnectionException<CursorResult<TResp>>(_endpoint);
            }

            return ThrowHelper.ObjectDisposedException<CursorResult<TResp>>(nameof(Channel));


            async ValueTask<IParserResult> ParseAsync<T>(MongoResponseMessage mongoResponse)
            {
                var reader = _reader!;
                switch (mongoResponse)
                {
                    case ResponseMsgMessage msgMessage:
                        if (SerializersMap.TryGetSerializer<T>(out var msgSerializer))
                        {
                            MsgBodyReader<T> bodyReader;
                            if (msgMessage.MsgHeader.PayloadType == 0)
                            {
                                bodyReader = new MsgType0BodyReader<T>(msgSerializer, msgMessage);
                            }
                            else if (msgMessage.MsgHeader.PayloadType == 1)
                            {
                                bodyReader = new MsgType1BodyReader<T>(msgSerializer, msgMessage);
                            }
                            else
                            {
                                return ThrowHelper.InvalidPayloadTypeException<CursorResult<T>>(msgMessage.MsgHeader
                                    .PayloadType);
                            }

                            _logger.ParsingMsgMessage(mongoResponse.Header.ResponseTo);
                            var result = await reader.ReadAsync(bodyReader, default).ConfigureAwait(false);
                            reader.Advance();
#if DEBUG
                            msgMessage.Consumed += bodyReader.Readed;
                            System.Diagnostics.Debug.Assert(msgMessage.Consumed == msgMessage.Header.MessageLength);
#endif
                            _logger.ParsingMsgCompleteMessage(mongoResponse.Header.ResponseTo);
                            return bodyReader.CursorResult;
                        }

                        return ThrowHelper.UnsupportedTypeException<CursorResult<T>>(typeof(T));
                    default:
                        return ThrowHelper.UnsupportedTypeException<CursorResult<T>>(typeof(T));
                }
            }
        }


        public async ValueTask<QueryResult<TResp>> GetMoreAsync<TResp>(GetMoreMessage message,
            CancellationToken cancellationToken)
        {
            if (_shutdownToken.IsCancellationRequested == false)
            {
                if (_writer is not null)
                {
                    // var completion = _completionMap.GetOrAdd(message.RequestNumber,
                    //     i => new TaskCompletionSourceWithCancellation<MongoResponseMessage>());

                    var completion = _completionMap.GetOrAdd(message.RequestNumber,
                        i => new ParserCompletion(new TaskCompletionSourceWithCancellation<IParserResult>(),
                            response => ParseAsync<TResp>(response)));
                    try
                    {
                        await _writer.WriteAsync(GetMoreWriter, message, cancellationToken).ConfigureAwait(false);
                        // Debug.WriteLine($"Sent query message '{message.RequestNumber}'");
                        // var response = await new ValueTask<MongoResponseMessage>(completionSource, completionSource.Version)
                        //     .ConfigureAwait(false);
                        // completionSource.Reset();
                        var result = await completion.CompletionSource.WaitWithCancellationAsync(cancellationToken)
                            .ConfigureAwait(false);

                        if (result is QueryResult<TResp> queryResult)
                        {
                            return queryResult;
                        }

                        return default!;
                        // return await ParseAsync<TResp>(response).ConfigureAwait(false);
                    }
                    finally
                    {
                        _completionMap.TryRemove(message.RequestNumber, out _);
                    }
                }

                return ThrowHelper.ConnectionException<QueryResult<TResp>>(_endpoint);
            }

            return ThrowHelper.ObjectDisposedException<QueryResult<TResp>>(nameof(Channel));


            async ValueTask<IParserResult> ParseAsync<T>(MongoResponseMessage mongoResponse)
            {
                var reader = _reader!;
                switch (mongoResponse)
                {
                    case ReplyMessage replyMessage:
                        if (SerializersMap.TryGetSerializer<T>(out var replySerializer))
                        {
                            var bodyReader = new ReplyBodyReader<T>(replySerializer, replyMessage);
                            var bodyResult = await reader.ReadAsync(bodyReader, _shutdownToken.Token)
                                .ConfigureAwait(false);
                            reader.Advance();
                            return bodyReader.Result;
                        }

                        return ThrowHelper.UnsupportedTypeException<QueryResult<T>>(typeof(T));
                    default:
                        return ThrowHelper.UnsupportedTypeException<QueryResult<T>>(typeof(T));
                }
            }
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

        #region NeedToRemove

        // public async ValueTask<TResp> SendAsync<TResp>(ReadOnlyMemory<byte> message,
        //     CancellationToken cancellationToken)
        // {
        //     if (_shutdownToken.IsCancellationRequested == false)
        //     {
        //         if (_writer is not null)
        //         {
        //             await _writer.WriteAsync(memoryWriter, message, cancellationToken).ConfigureAwait(false);
        //
        //             var completion = _completionMap.GetOrAdd(message.RequestNumber,
        //                 i => new TaskCompletionSource<MongoResponseMessage>());
        //             // var response = await new ValueTask<MongoResponseMessage>(completionSource, completionSource.Version)
        //             //     .ConfigureAwait(false);
        //             // completionSource.Reset();
        //             var response = await completion.Task.ConfigureAwait(false);
        //             _completionMap.TryRemove(message.RequestNumber, out _);
        //             return await ParseAsync<TResp>(response).ConfigureAwait(false);
        //         }
        //
        //         return ThrowHelper.ConnectionException<TResp>(_endpoint);
        //     }
        //
        //     return ThrowHelper.ObjectDisposedException<TResp>(nameof(Channel));
        //
        //
        //     async ValueTask<T> ParseAsync<T>(MongoResponseMessage message)
        //     {
        //         var reader = _reader!;
        //         switch (message)
        //         {
        //             case ReplyMessage replyMessage:
        //                 if (SerializersMap.TryGetSerializer<T>(out var replySerializer))
        //                 {
        //                     var bodyReader = new ReplyBodyReader<T>(replySerializer);
        //                     var bodyResult = await reader.ReadAsync(bodyReader, _shutdownToken.Token)
        //                         .ConfigureAwait(false);
        //                     reader.Advance();
        //                     return bodyResult.Message;
        //                 }
        //
        //                 return ThrowHelper.UnsupportedTypeException<T>(typeof(T));
        //             default:
        //                 return ThrowHelper.UnsupportedTypeException<T>(typeof(T));
        //         }
        //     }
        // }

        #endregion

        private class ParserCompletion
        {
            public ParserCompletion(TaskCompletionSourceWithCancellation<IParserResult> completionSource,
                Func<MongoResponseMessage, ValueTask<IParserResult>> parseAsync)
            {
                CompletionSource = completionSource;
                ParseAsync = parseAsync;
            }

            public TaskCompletionSourceWithCancellation<IParserResult> CompletionSource { get; }
            public Func<MongoResponseMessage, ValueTask<IParserResult>> ParseAsync { get; }
        }
    }
}