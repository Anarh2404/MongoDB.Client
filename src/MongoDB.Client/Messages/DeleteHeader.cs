﻿using MongoDB.Client.Bson.Serialization.Attributes;

namespace MongoDB.Client.Messages
{
    [BsonSerializable]
    public class DeleteHeader
    {
        [BsonElement("delete")]
        public string Delete { get; set; }

        [BsonElement("ordered")]
        public bool Ordered { get; set; }

        [BsonElement("$db")]
        public string Db { get; set; }

        [BsonElement("lsid")]
        public SessionId Lsid { get; set; }
    }
}