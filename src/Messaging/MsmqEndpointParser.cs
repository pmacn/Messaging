using System;
using System.Messaging;

namespace Messaging
{
    internal static class MsmqEndpointParser
    {
        internal static string GetQueuePath(BusEndpoint endpoint)
        {
            return String.Format(@"{0}\private$\{1}", endpoint.MachineName, endpoint.QueueName);
        }

        internal static BusEndpoint GetEndpoint(MessageQueue messageQueue)
        {
            var queueName = messageQueue.QueueName.Substring(messageQueue.QueueName.LastIndexOf('\\') + 1);
            return new BusEndpoint(messageQueue.MachineName, queueName);
        }
    }
}