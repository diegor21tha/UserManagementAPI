using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace UserManagementAPI.Services
{
    public class RequestCountsStore
    {
        private readonly ConcurrentDictionary<string, long> _counts = new();

        public void Increment(string key)
        {
            _counts.AddOrUpdate(key, 1, (_, v) => v + 1);
        }

        public Dictionary<string, long> GetCounts()
        {
            return _counts.ToDictionary(kv => kv.Key, kv => kv.Value);
        }
    }
}
