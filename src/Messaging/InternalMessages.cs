using System;

namespace Messaging
{
    [Serializable]
    public class StartSubscriptionRequest
    {
        public Subscription Subscription { get; private set; }

        public StartSubscriptionRequest(Subscription subscription)
        {
            Subscription = subscription;
        }
    }

    [Serializable]
    public class EndSubscriptionRequest
    {
        public EndSubscriptionRequest(Subscription subscription)
        {
            Subscription = subscription;
        }

        public Subscription Subscription { get; private set; }
    }

    [Serializable]
    public class SubscriptionStarted
    {
        public SubscriptionStarted(Subscription subscription)
        {
            Subscription = subscription;
        }

        public Subscription Subscription { get; private set; }
    }

    [Serializable]
    public class SubscriptionEnded
    {
        public SubscriptionEnded(Subscription subscription)
        {
            Subscription = subscription;
        }

        public Subscription Subscription { get; private set; }
    }
}