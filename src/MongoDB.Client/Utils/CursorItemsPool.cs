﻿using System.Collections.Generic;
using Microsoft.Extensions.ObjectPool;

namespace MongoDB.Client.Utils
{
    public static class CursorItemsPool<TPool>
    {
        public static readonly ObjectPool<List<TPool>> Pool =
            new DefaultObjectPool<List<TPool>>(new PooledListPolicy<TPool>());
        
        private sealed class PooledListPolicy<T> : IPooledObjectPolicy<List<T>>
        {
            public List<T> Create()
            {
                return new List<T>();
            }

            public bool Return(List<T> obj)
            {
                obj.Clear();
                return true;
            }
        }
    }
}