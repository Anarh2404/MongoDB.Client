﻿namespace MongoDB.Client
{
    public class MongoDatabase
    {
        private readonly ChannelsPool _channelsPool;
        public MongoClient Client { get; }
        public string Name { get; }

        internal MongoDatabase(MongoClient client, string name, ChannelsPool channelsPool)
        {
            _channelsPool = channelsPool;
            Client = client;
            Name = name;
        }

        public MongoCollection<T> GetCollection<T>(string name)
        {
            return new MongoCollection<T>(this, name, _channelsPool);
        }
    }
}
