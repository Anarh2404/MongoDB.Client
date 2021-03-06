﻿using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MongoDB.Client.Bson.Document
{
    [DebuggerDisplay("{Value}", Name = "{Name}")]
    public readonly struct BsonElement : IEquatable<BsonElement>
    {
        public readonly BsonDocument Parent { get; }
        public readonly BsonElementType Type { get; }
        public readonly string Name { get; }

        public object? Value { get; }

        private BsonElement(BsonDocument parent, BsonElementType type, string name, object? value)
        {
            Parent = parent;
            Type = type;
            Name = name;
            Value = value;
        }
        //public unsafe T As<T>() where T: struct
        //{
        //    var ptr = Unsafe.AsPointer(ref _value);
        //    var val = Unsafe.AsRef<T>(ptr);
        //    return val;
        //}
        //public static BsonElement Create(BsonDocument parent, string name, byte value)
        //{
        //    return new BsonElement(parent, BsonElementType.Byte, name, value);
        //}
        //public static BsonElement Create(BsonDocument parent, string name, short value)
        //{
        //    return new BsonElement(parent, BsonElementType.Int16, name, value);
        //}
        public static BsonElement Create(BsonDocument parent, string name, int value)
        {
            return new BsonElement(parent, BsonElementType.Int32, name, value);
        }


        public static BsonElement Create(BsonDocument parent, string name, long value)
        {
            return new BsonElement(parent, BsonElementType.Int64, name, value);
        }


        public static BsonElement Create(BsonDocument parent, string name, string? value)
        {
            if (value is not null)
            {
                return new BsonElement(parent, BsonElementType.String, name, value);
            }
            return new BsonElement(parent, BsonElementType.Null, name, null);
        }


        public static BsonElement Create(BsonDocument parent, string name, double value)
        {
            return new BsonElement(parent, BsonElementType.Double, name, value);
        }


        public static BsonElement Create(BsonDocument parent, string name, BsonDocument? value)
        {
            if (value is not null)
            {
                return new BsonElement(parent, BsonElementType.Document, name, value);
            }
            return new BsonElement(parent, BsonElementType.Null, name, null);
        }


        public static BsonElement Create(BsonDocument parent, string name, BsonObjectId value)
        {
            return new BsonElement(parent, BsonElementType.ObjectId, name, value);
        }

        public static BsonElement Create(BsonDocument parent, string name, BsonTimestamp value)
        {
            return new BsonElement(parent, BsonElementType.Timestamp, name, value);
        }

        public static BsonElement Create(BsonDocument parent, string name, BsonBinaryData value)
        {
            return new BsonElement(parent, BsonElementType.BinaryData, name, value);
        }


        public static BsonElement Create(BsonDocument parent, string name, DateTimeOffset value)
        {
            return new BsonElement(parent, BsonElementType.UtcDateTime, name, value);
        }


        public static BsonElement Create(BsonDocument parent, string name, bool value)
        {
            return new BsonElement(parent, BsonElementType.Boolean, name, value);
        }


        public static BsonElement Create(BsonDocument parent, string name)
        {
            return new BsonElement(parent, BsonElementType.Null, name, null);
        }


        public static BsonElement CreateArray(BsonDocument parent, string name, BsonDocument? root)
        {
            if (root is not null)
            {
                return new BsonElement(parent, BsonElementType.Array, name, root);
            }
            return new BsonElement(parent, BsonElementType.Null, name, null);
        }

        public BsonDocument? AsBsonDocument => (BsonDocument?)Value;

        public override string ToString()
        {
            return Name + ": " + (Value is not null ? Value.ToString() : "null");
        }

        public override bool Equals(object? obj)
        {
            return obj is BsonElement element && Equals(element);
        }

        public bool Equals(BsonElement element)
        {
            return Type == element.Type && Name == element.Name && EqualityComparer<object?>.Default.Equals(Value, element.Value);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Name, Value);
        }
    }
}
