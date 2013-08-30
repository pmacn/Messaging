using System;
using System.Linq;
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
            {
                // TODO : Should be a different exception?
                throw new TargetEndpointNotFoundException(String.Format("There is no registered endpoint for messages of type {0}", messageType.Name));
            }

            return endpoint;
        }

        internal BusEndpoint[] For(Type messageType)
        {
            return _endpoints.Where(kvp => kvp.Key.IsAssignableFrom(messageType)).Select(kvp => kvp.Value).ToArray();
        }
    }
}