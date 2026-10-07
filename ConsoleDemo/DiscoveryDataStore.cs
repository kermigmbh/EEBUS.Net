using EEBUS.SPINE.Commands;
using System.Collections.Concurrent;

namespace ConsoleDemo
{
    public static class DiscoveryDataStore
    {
        private static readonly ConcurrentDictionary<string, NodeManagementDetailedDiscoveryDataType> _data = new();

        public static IReadOnlyDictionary<string, NodeManagementDetailedDiscoveryDataType> All => _data;

        public static void Set(string deviceId, NodeManagementDetailedDiscoveryDataType data)
        {
            _data[deviceId] = data;
        }

        public static NodeManagementDetailedDiscoveryDataType? Get(string deviceId)
        {
            return _data.TryGetValue(deviceId, out var data) ? data : null;
        }

        public static bool Remove(string deviceId) => _data.TryRemove(deviceId, out _);

        public static void Clear() => _data.Clear();
    }
}
