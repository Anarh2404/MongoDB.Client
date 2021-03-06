﻿using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Client.Bson.Document;
using MongoDB.Client.Tests.Serialization.TestModels;
using Xunit;

namespace MongoDB.Client.Tests.Serialization
{
    public class GeneratedSerializerTests : BaseSerialization
    {
        [Fact]
        public async Task IgnoreTest()
        {
            var doc = new ModelWithIgnore
            {
                Field = "Field",
                IgnoredField = "IgnoredField"
            };
            SerializersMap.TryGetSerializer<ModelWithIgnore>(out var serializer);


            var result = await RoundTripAsync(doc, serializer);


            Assert.Equal(doc.Field, result.Field);
            Assert.Null(result.IgnoredField);
        }
        [Fact]
        public async Task SerializationDeserializationGenerated()
        {
            var lstModel = new ModelForGenerated.ListModel()
            {
                Strings = new List<string>(){"StringValue1" , "StringValue2", "StringValue3"},
                Bools = new List<bool>() {true, false, true},
                BsonObjectIds = new List<BsonObjectId>() {new BsonObjectId("5f987814bf344ec7cc57294b"), new BsonObjectId("6f987814bf344ec7cc57294b")},
                Documents = new List<BsonDocument>(){ new BsonDocument("Document1", "StingElement1")},
                Doubles = new List<double>(){41254.25497, 34879.3248},
                Ints = new List<int>() {1,4,9,7,5},
                Longs = new List<long>(){412541452, 5632552365, 7854774587, 8569885698 },
                Items = new List<ModelForGenerated.ListItem>()
                {
                    new ModelForGenerated.ListItem("ModelForGenerated.ListItem1"),
                    new ModelForGenerated.ListItem("ModelForGenerated.ListItem2"),
                    new ModelForGenerated.ListItem("ModelForGenerated.ListItem3")
                }
            };
            var doc = new ModelForGenerated
            {
                DoubleValue =  52456478.24587874,
                StringValue =  "GeneratorTest",
                BooleanValue =  true,
                BsonDocumentValue =  new BsonDocument("BspnDocumentValue", "StringField"),
                BsonObjectIdValue =  new BsonObjectId("5f987814bf342ec7cc57294b"),
                IntValue = 479341564,
                LongValue =  9713984265,
                List = lstModel,
            };
            SerializersMap.TryGetSerializer<ModelForGenerated>(out var serializer);
            var result = await RoundTripAsync(doc, serializer);


            Assert.Equal(doc, result);
        }


    }
}