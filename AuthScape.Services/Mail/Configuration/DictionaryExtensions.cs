using System.Collections.Generic;

namespace AuthScape.Services.Mail.Configuration
{
    internal static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default)
        {
            if (dictionary == null || key == null)
            {
                return defaultValue;
            }

            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }
}
