using System;
using System.Collections.Concurrent;

namespace Messaging
{
    public class TargetEndpoints
    {
        private readonly ConcurrentDictionary<Type, BusEndpoint> _endpoints = new ConcurrentDictionary<Type, BusEndpoint>();

        internal TargetEndpoints() { }

        public void SetFor<TMessage>(BusEndpoint endpoint)
        {
            var messageType = typeof(TMessage);
            _endpoints.AddOrUpdate(messageType, endpoint, (t, e) => endpoint);
        }

        public void RemoveFor<TMessage>()
        {
            var messageType = typeof(TMessage);
            BusEndpoint removedEndpoint;
            _endpoints.TryRemove(messageType, out removedEndpoint);
        }

        internal BusEndpoint GetFor(Type messageType)
        {
            BusEndpoint endpoint;
            if (!_endpoints.TryGetValue(messageType, out endpoint))
                throw new TargetEndpointNotFoundException(); // TODO : Should be a different exception

            return endpoint;
        }
    }
}