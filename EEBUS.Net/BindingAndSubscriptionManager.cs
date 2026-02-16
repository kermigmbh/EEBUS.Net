using EEBUS.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net
{
    public class BindingAndSubscriptionManager(Connection _connection)
    {
        private ConcurrentBag<BindingSubscriptionInfo> _subsriptions = new ();

        

        public bool TryAddOrUpdateClientBinding(AddressType clientAddress, AddressType serverAddress, string serverFeatureType)
        {
            var localEntity = _connection.Local.Entities.FirstOrDefault(e => e.Index.SequenceEqual(serverAddress.entity));
            var localFeature = localEntity?.Features.FirstOrDefault(f => f.Index == serverAddress.feature && /*f.Type == serverFeatureType &&*/ f.Role == "server");

            var remoteEntity = _connection.Remote?.Entities.FirstOrDefault(e => e.Index.SequenceEqual(clientAddress.entity));
            var remoteFeature = remoteEntity?.Features.FirstOrDefault(f => f.Index == clientAddress.feature && /*f.Type == serverFeatureType &&*/ f.Role == "client");

            if (localFeature == null || remoteFeature == null)
            {
                return false;
            }


            var entry = _subsriptions.FirstOrDefault(b => 
                            b.serverFeatureType == serverFeatureType &&
                            //b.clientAddress.feature == bindingSubscription.clientAddress.feature &&
                            //b.clientAddress.entity.SequenceEqual(b.clientAddress.entity) &&
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
                    State = BindingSubscriptionState.Binding
                };
                entry.clientAddress = clientAddress;
                _subsriptions.Add(entry);   
            }
            else
            {
                entry.clientAddress = clientAddress;
                entry.State = BindingSubscriptionState.Binding;
            }
            return true;
        }

        public bool TryAddOrUpdateSubscription(AddressType clientAddress, AddressType serverAddress, string serverFeatureType)
        {
            var entry = _subsriptions.FirstOrDefault(b =>
                            //b.serverFeatureType == serverFeatureType &&
                            b.clientAddress.feature == clientAddress.feature &&
                            b.clientAddress.entity.SequenceEqual(clientAddress.entity) &&
                            b.serverAddress.feature == serverAddress.feature &&
                            b.serverAddress.entity.SequenceEqual(serverAddress.entity) && 
                            b.State == BindingSubscriptionState.Binding
                            );

            if (entry == null)
            {
                return false;
            }
            else
            {  
                entry.State = BindingSubscriptionState.Subscription;
            }
            return true;
        }


        public BindingSubscriptionState GetState(AddressType clientAddress, AddressType serverAddress)
        {
            var entry = _subsriptions.FirstOrDefault(b =>
                            //b.serverFeatureType == serverFeatureType &&
                            b.clientAddress.feature == clientAddress.feature &&
                            b.clientAddress.entity.SequenceEqual(clientAddress.entity) &&
                            b.serverAddress.feature == serverAddress.feature &&
                            b.serverAddress.entity.SequenceEqual(serverAddress.entity) &&
                            b.State == BindingSubscriptionState.Binding
                            );

            return entry?.State ?? BindingSubscriptionState.None;

        }

    }






    public enum BindingSubscriptionState
    {
        None,
        Binding,
        Subscription
    }

    public class BindingSubscriptionInfo
    {
        public BindingSubscriptionState State { get; set; }
        public AddressType clientAddress { get; set; }

        public AddressType serverAddress { get; set; }

        public string serverFeatureType { get; set; }
    }
}
