using System;
using System.Runtime.Serialization;

namespace Messaging
{
    [Serializable]
    [DataContract(Namespace = "ServiceBusInternalMessages")]
    public class StartSubscriptionRequest
    {
        public StartSubscriptionRequest(Subscription subscription)
        {
            Subscription = subscription;
        }

        [DataMember(Order = 1)]
        public Subscription Subscription { get; private set; }
    }

    [Serializable]
    [DataContract(Namespace = "ServiceBusInternalMessages")]
    public class EndSubscriptionRequest
    {
        public EndSubscriptionRequest(Subscription subscription)
        {
            Subscription = subscription;
        }

        [DataMember(Order = 1)]
        public Subscription Subscription { get; private set; }
    }

    [Serializable]
    [DataContract(Namespace = "ServiceBusInternalMessages")]
    public class SubscriptionStarted
    {
        public SubscriptionStarted(Subscription subscription)
        {
            Subscription = subscription;
        }

        [DataMember(Order = 1)]
        public Subscription Subscription { get; private set; }
    }

    [Serializable]
    [DataContract(Namespace = "ServiceBusInternalMessages")]
    public class SubscriptionEnded
    {
        public SubscriptionEnded(Subscription subscription)
        {
            Subscription = subscription;
        }

        [DataMember(Order = 1)]
        public Subscription Subscription { get; private set; }
    }
}