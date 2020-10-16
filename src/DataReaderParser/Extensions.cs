using System;
using System.Collections.Generic;

namespace DataReaderParser
{
    public static class Extensions
    {
        public static O FeedTo<I, O>(this I that, Func<I, O> f) => f(that);

        public static V TryGetValueOrDefault<K, V>(this IReadOnlyDictionary<K, V> that, K key)
        {
            that.TryGetValue(key, out var value);
            return value;
        }

        public static void Add<K, V>(this IDictionary<K, V> that, KeyValuePair<K, V> kvp) =>
            that.Add(kvp.Key, kvp.Value);

        public static IEnumerable<T> YieldOne<T>(this T that) => new[] { that };
    }
}
