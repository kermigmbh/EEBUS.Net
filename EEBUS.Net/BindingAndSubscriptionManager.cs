using EEBUS.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net
{
    public class BindingAndSubscriptionManager(Connection _connection)
    {
        private readonly List<BindingSubscriptionInfo> _subsriptions = new();
        private readonly List<BindingSubscriptionInfo> _bindings = new();
        private readonly Lock _lock = new();



        public bool IsKnownFeature(AddressType clientAddress, AddressType serverAddress, string serverFeatureType)
        {
            var localEntity = _connection.Local.Entities.FirstOrDefault(e => e.Index.SequenceEqual(serverAddress.entity));
            var localFeature = localEntity?.Features.FirstOrDefault(f => f.Index == serverAddress.feature && /*f.Type == serverFeatureType &&*/ f.Role is "server" or "special" );

            var remoteEntity = _connection.Remote?.Entities.FirstOrDefault(e => e.Index.SequenceEqual(clientAddress.entity));
            var remoteFeature = remoteEntity?.Features.FirstOrDefault(f => f.Index == clientAddress.feature && /*f.Type == serverFeatureType &&*/ f.Role is "client" or "special");

            if (localFeature == null || remoteFeature == null)
            {
                return false;
            }
            return true;
        }

        public bool TryAddOrUpdateClientBinding(AddressType clientAddress, AddressType serverAddress, string serverFeatureType)
        {
            if (!IsKnownFeature(clientAddress, serverAddress, serverFeatureType))
            {
                return false;
            }

            lock (_lock)
            {
                var entry = _bindings.FirstOrDefault(b =>
                                b.serverFeatureType == serverFeatureType &&
                                b.serverAddress.feature == serverAddress.feature &&
                                b.serverAddress.entity.SequenceEqual(serverAddress.entity)
                                );

                if (entry == null)
                {
                    entry = new BindingSubscriptionInfo()
                    {
                        clientAddress = clientAddress,
                        serverAddress = serverAddress,
                        serverFeatureType = serverFeatureType,
                    };
                    _bindings.Add(entry);
                }
                else
                {
                    entry.clientAddress = clientAddress;
                }
            }
            return true;
        }

        public bool TryAddOrUpdateSubscription(AddressType clientAddress, AddressType serverAddress, string serverFeatureType)
        {
            if (!IsKnownFeature(clientAddress, serverAddress, serverFeatureType))
            {
                return false;
            }

            lock (_lock)
            {
                var entry = _subsriptions.FirstOrDefault(b =>
                                b.serverAddress.feature == serverAddress.feature &&
                                b.serverAddress.entity.SequenceEqual(serverAddress.entity)
                                );
                if (entry == null)
                {
                    entry = new BindingSubscriptionInfo()
                    {
                        clientAddress = clientAddress,
                        serverAddress = serverAddress,
                        serverFeatureType = serverFeatureType,
                    };
                    _subsriptions.Add(entry);
                }
                else
                {
                    entry.clientAddress = clientAddress;
                }
            }
            return true;
        }


        public bool HasBinding(AddressType clientAddress, AddressType serverAddress/*, string serverFeatureType*/)
        {
            lock (_lock)
            {
                return _bindings.Any(b =>
                                b.clientAddress.feature == clientAddress.feature &&
                                b.clientAddress.entity.SequenceEqual(clientAddress.entity) &&
                                b.serverAddress.feature == serverAddress.feature &&
                                b.serverAddress.entity.SequenceEqual(serverAddress.entity)
                                );
            }
        }
        public bool HasSubscription(AddressType clientAddress, AddressType serverAddress/*, string serverFeatureType*/)
        {
            lock (_lock)
            {
                return _subsriptions.Any(b =>
                                b.clientAddress.feature == clientAddress.feature &&
                                b.clientAddress.entity.SequenceEqual(clientAddress.entity) &&
                                b.serverAddress.feature == serverAddress.feature &&
                                b.serverAddress.entity.SequenceEqual(serverAddress.entity)
                                );
            }
        }

        public IEnumerable<AddressType> GetSubscriptionsByServerAddress(AddressType serverAddress)
        {
            lock (_lock)
            {
                // Materialize inside the lock so the caller can enumerate safely.
                return _subsriptions
                    .Where(subscription => subscription.serverAddress == serverAddress)
                    .Select(subscription => subscription.clientAddress)
                    .ToList();
            }
        }
    }






    //public enum BindingSubscriptionState
    //{
    //    None,
        
    //}

    public class BindingSubscriptionInfo
    {
        //public BindingSubscriptionState State { get; set; }
        public required AddressType clientAddress { get; set; }

        public required AddressType serverAddress { get; set; }

        public required string serverFeatureType { get; set; }
    }
}
