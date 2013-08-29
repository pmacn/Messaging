using System;
using System.Collections.Generic;
using System.Linq;

namespace Messaging
{
    internal class Subscriptions
    {
        public List<Subscription> _subscribers = new List<Subscription>();

        public IEnumerable<Subscription> For(Type messageType)
        {
            lock (_subscribers)
            {
                return _subscribers.Where(s => s.MessageType.IsAssignableFrom(messageType)).ToList();
            }
        }

        public void Add(Subscription subscriptionToAdd)
        {
            lock (_subscribers)
            {
                _subscribers.RemoveAll(s => s.MessageType == subscriptionToAdd.MessageType && s.SubscriberEndpoint == subscriptionToAdd.SubscriberEndpoint);
                _subscribers.Add(subscriptionToAdd);
            }
        }

        internal void Remove(params Subscription[] subscriptionsToRemove)
        {
            foreach (var subscription in subscriptionsToRemove)
                _subscribers.Remove(subscription);
        }

        internal void Remove(Type messageType, BusEndpoint publisherEndpoint)
        {

        }
    }
}

