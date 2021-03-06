﻿using System;
using System.Collections.Generic;
using System.Threading;
using MongoDB.Client.Bson.Document;
using MongoDB.Client.Protocol.Messages;
using MongoDB.Client.Utils;

namespace MongoDB.Client
{
    public class Cursor<T> : IAsyncEnumerable<T>
    {
        private readonly ChannelsPool _channelPool;
        private readonly BsonDocument _filter;
        private readonly CollectionNamespace _collectionNamespace;
        private readonly BsonDocument _sessionId;
        private int _limit;

        internal Cursor(ChannelsPool channelPool, BsonDocument filter, CollectionNamespace collectionNamespace)
            : this(channelPool, filter, collectionNamespace, Guid.NewGuid())
        {
        }

        internal Cursor(ChannelsPool channelPool, BsonDocument filter, CollectionNamespace collectionNamespace,
            Guid sessionId)
        {
            _channelPool = channelPool;
            _filter = filter;
            _collectionNamespace = collectionNamespace;
            _sessionId = new BsonDocument("id", BsonBinaryData.Create(sessionId));
        }

        internal void AddLimit(int limit)
        {
            _limit = limit;
        }

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            var channel = await _channelPool.GetChannelAsync(cancellationToken).ConfigureAwait(false);
            var requestNum = channel.GetNextRequestNumber();
            var requestDocument = CreateFindRequest(_filter);
            var request = new MsgMessage(requestNum, _collectionNamespace.FullName, requestDocument);
            var result = await channel.GetCursorAsync<T>(request, cancellationToken).ConfigureAwait(false);

            foreach (var item in result.MongoCursor.Items)
            {
                yield return item;
            }

            CursorItemsPool<T>.Pool.Return(result.MongoCursor.Items);
            long cursorId = result.MongoCursor.Id;
            while (cursorId != 0)
            {
                requestNum = channel.GetNextRequestNumber();
                requestDocument = CreateGetMoreRequest(cursorId);
                request = new MsgMessage(requestNum, _collectionNamespace.FullName, requestDocument);
                var getMoreResult = await channel.GetCursorAsync<T>(request, cancellationToken).ConfigureAwait(false);
                cursorId = getMoreResult.MongoCursor.Id;
                foreach (var item in getMoreResult.MongoCursor.Items)
                {
                    yield return item;
                }
                CursorItemsPool<T>.Pool.Return(getMoreResult.MongoCursor.Items);
            }
        }

        private BsonDocument CreateFindRequest(BsonDocument filter)
        {
            return new BsonDocument
            {
                {"find", _collectionNamespace.CollectionName},
                {"filter", filter},
                {"limit", _limit, _limit > 0},
                {"$db", _collectionNamespace.DatabaseName},
                {"lsid", _sessionId}
            };
        }

        private BsonDocument CreateGetMoreRequest(long cursorId)
        {
            return new BsonDocument
            {
                {"getMore", cursorId},
                {"collection", _collectionNamespace.CollectionName},
                {"$db", _collectionNamespace.DatabaseName},
                {"lsid", _sessionId}
            };
        }
    }
}