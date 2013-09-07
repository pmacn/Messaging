using System;
using System.Messaging;

namespace Messaging
{
    internal static class MsmqEndpointParser
    {
        internal static string GetLocalQueuePath(BusEndpoint endpoint)
        {
            if (IsLocalhost(endpoint.MachineName))
                throw new InvalidLocalQueueException("MSMQ can only listen to local queues");
            return String.Format(@".\private$\{0}", endpoint.QueueName);
        }

        internal static string GetQueuePath(BusEndpoint endpoint)
        {
            if (String.IsNullOrWhiteSpace(endpoint.QueueName))
                throw new InvalidOperationException("Queue name cannot be null or empty");
            if (IsLocalhost(endpoint.MachineName))
                return string.Format(@".\private$\{0}", endpoint.QueueName);
            return String.Format(@"{0}\private$\{1}", endpoint.MachineName, endpoint.QueueName);
        }

        internal static string GetErrorQueuePath(BusEndpoint endpoint) { return GetLocalQueuePath(endpoint) + "_errors"; }

        private static bool IsLocalhost(string machineName)
        {
            if (String.IsNullOrWhiteSpace(machineName))
                return true;
            return machineName.Equals("localhost", StringComparison.OrdinalIgnoreCase) || machineName.Equals(".", StringComparison.OrdinalIgnoreCase);
        }

        internal static BusEndpoint GetEndpoint(MessageQueue messageQueue)
        {
            var queueName = messageQueue.QueueName.Substring(messageQueue.QueueName.LastIndexOf('\\') + 1);
            return new BusEndpoint(messageQueue.MachineName, queueName);
        }
    }
}