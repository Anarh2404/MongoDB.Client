﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;
using MongoDB.Client.Network;
using MongoDB.Client.Bson.Document;
using MongoDB.Client.Bson.Reader;
using MongoDB.Client.Bson.Serialization;

namespace MongoDB.Test
{
    class Program
    {
        static async Task Main(string[] args)
        {

            
            //var factory = new NetworkConnectionFactory();
            //var connection = await factory.ConnectAsync(new DnsEndPoint("centos0.mshome.net", 27017));
            //var seq = await connection.Pipe.Input.ReadAsync();
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
            Test();

        }
        static unsafe void Test()
        {
            ReadOnlyMemory<byte> file = File.ReadAllBytes("../../../ReaderTestCollection.bson");
            //ReadOnlyMemory<byte> file = File.ReadAllBytes("../../../Meteoritelandings.bson");
            var reader = new MongoDBBsonReader(file);
            //IBsonSerializable serializator = new MongoDB.Client.Test.Generated.NasaMeteoriteLandingGeneratedSerializator();
            IBsonSerializable serializator = new MongoDB.Client.Test.Generated.DocumentObjectGeneratedSerializator();
            serializator.TryParse(ref reader, out var doc);
            //reader.TryParseDocument(null, out var document);
            //reader.TryParseDocument(null, out var document1);
            //reader.TryParseDocument(null, out var document2);
            //reader.TryParseDocument(null, out var document3);
            //reader.TryParseDocument(null, out var document4);
            //reader.TryParseDocument(null, out var document5);
            
        }
    }
}
