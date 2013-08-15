
namespace Messaging
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;

    public interface IBusPublisher
    {
        SubscriberEndpointNotFoundPolicy SubscriberNotFoundPolicy { get; set; }

        /// <summary>
        /// Configures the Service bus to handle subscription commands for the specified type of messages.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        void HandleSubscriptionsFor<TMessage>()
            where TMessage : class;

        /// <summary>
        /// Publishes a message to all subscribers that have registered for the specific type of message.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <param name="message"></param>
        void Publish<TMessage>(TMessage message)
            where TMessage : class;
    }

    public interface IBusSubscriber
    {
        /// <summary>
        /// Sends a command to the speficied endpoint requesting to receive messages of the specified type.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <param name="endpointName"></param>
        /// <param name="handler"></param>
        void SubscribeTo<TMessage>(QueueEndpoint endpointName, Action<TMessage> handler)
            where TMessage : class;

        /// <summary>
        /// Sends a command to the specified endpoint requesting to stop receiving messages of the specified type.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <param name="endpointName"></param>
        void Unsubscribe<TMessage>(QueueEndpoint endpointName)
            where TMessage : class;
    }

    public interface IServiceBus : IBusPublisher, IBusSubscriber { }

    public enum SubscriberEndpointNotFoundPolicy
    {
        Keep,
        Remove
    }

    public class ServiceBus : IServiceBus
    {
        private readonly IMessageBus _messageBus;

        private readonly List<SubscriptionDescriptor> _subscriptions = new List<SubscriptionDescriptor>();

        private readonly List<RuntimeTypeHandle> _subscriptionsHandled = new List<RuntimeTypeHandle>();

        private readonly List<SubscriptionConfirmation> _activeSubscriptions = new List<SubscriptionConfirmation>();

        public ServiceBus(IMessageBus simpleBus)
        {
            Contract.Requires(simpleBus != null, "simpleBus cannot be null");

            SubscriberNotFoundPolicy = SubscriberEndpointNotFoundPolicy.Remove;
            _messageBus = simpleBus;
            _messageBus.RegisterMessageHandler<StartSubscriptionCommand>(HandleSubscriptionStart);
            _messageBus.RegisterMessageHandler<EndSubscriptionCommand>(HandleSubscriptionEnd);
            _messageBus.Start();
        }

        public SubscriberEndpointNotFoundPolicy SubscriberNotFoundPolicy { get; set; }

        public void HandleSubscriptionsFor<TMessage>()
            where TMessage : class
        {
            _subscriptionsHandled.Add(typeof(TMessage).TypeHandle);
        }

        public void Publish<TMessage>(TMessage message)
            where TMessage : class
        {
            var messageTypeHandle = typeof(TMessage).TypeHandle;
            if (!_subscriptionsHandled.Contains(messageTypeHandle))
                throw new InvalidOperationException();

            var targetEndpointsNotFound = new List<SubscriptionDescriptor>();
            foreach (var subscription in _subscriptions.Where(s => s.MessageTypeHandle.Equals(messageTypeHandle)))
            {
                try
                {
                    _messageBus.Send(message, subscription.Endpoint);
                }
                catch (TargetEndpointNotFoundException ex)
                {
                    targetEndpointsNotFound.Add(subscription);
                }
            }

            if(SubscriberNotFoundPolicy == SubscriberEndpointNotFoundPolicy.Remove)
                _subscriptions.RemoveAll(targetEndpointsNotFound.Contains);
        }

        public void SubscribeTo<TMessage>(QueueEndpoint publisherEndpoint, Action<TMessage> handler)
            where TMessage : class
        {
            _messageBus.Start();
            _messageBus.RegisterMessageHandler<TMessage>(handler);
            var subCommand = new StartSubscriptionCommand { MessageTypeHandle = typeof(TMessage).TypeHandle, SubscriberEndpoint = _messageBus.LocalEndpoint };
            _messageBus.Send(subCommand, publisherEndpoint);
        }

        public void Unsubscribe<TMessage>(QueueEndpoint publisherEndpoint)
            where TMessage : class
        {
            var messageTypeHandle = typeof(TMessage).TypeHandle;
            var subscription = _activeSubscriptions.SingleOrDefault(s => s.MessageTypeHandle.Equals(messageTypeHandle) && s.PublisherEndpoint.Equals(publisherEndpoint));
            if (subscription == null)
                return;

            var subCommand = new EndSubscriptionCommand { SubscriptionToken = subscription.SubscriptionToken };
            _messageBus.Send(subCommand, publisherEndpoint);
            _activeSubscriptions.Remove(subscription);
        }

        private void HandleSubscriptionConfirmed(SubscriptionConfirmation command)
        {
            _activeSubscriptions.Add(command);
        }

        private void HandleSubscriptionStart(StartSubscriptionCommand command)
        {
            if (!_subscriptionsHandled.Contains(command.MessageTypeHandle))
                return; // TODO : Log subscription attempt

            var subscriber = new SubscriptionDescriptor { Endpoint = command.SubscriberEndpoint, MessageTypeHandle = command.MessageTypeHandle, SubscriptionToken = Guid.NewGuid() };
            _subscriptions.Add(subscriber);
            SendConfirmation(subscriber);
        }

        private void SendConfirmation(SubscriptionDescriptor subscriber)
        {
            _messageBus.Send(new SubscriptionConfirmation
            {
                MessageTypeHandle = subscriber.MessageTypeHandle,
                SubscriptionToken = subscriber.SubscriptionToken,
                PublisherEndpoint = _messageBus.LocalEndpoint
            }, subscriber.Endpoint);
        }

        private void HandleSubscriptionEnd(EndSubscriptionCommand command)
        {
            _subscriptions.RemoveAll(s => s.SubscriptionToken == command.SubscriptionToken);
        }

        public void Dispose()
        {
            if (_messageBus != null)
            {
                _messageBus.Dispose();
            }

            _subscriptions.Clear();
        }
    }

    public class SubscriptionDescriptor
    {
        public RuntimeTypeHandle MessageTypeHandle { get; set; }
        public QueueEndpoint Endpoint { get; set; }
        public Guid SubscriptionToken { get; set; }
    }

    [Serializable]
    public class StartSubscriptionCommand
    {
        public RuntimeTypeHandle MessageTypeHandle { get; set; }

        private QueueEndpoint _subscriberEndpoint;

        public QueueEndpoint SubscriberEndpoint
        {
            get { return _subscriberEndpoint; }
            set
            {
                _subscriberEndpoint =
                    value.MachineName == "" ?
                    new QueueEndpoint(System.Environment.MachineName, value.QueueName) :
                    value;
            }
        }
    }

    [Serializable]
    public class EndSubscriptionCommand
    {
        public Guid SubscriptionToken { get; set; }
    }

    [Serializable]
    public class SubscriptionConfirmation
    {
        public RuntimeTypeHandle MessageTypeHandle { get; set; }
        public QueueEndpoint PublisherEndpoint { get; set; }
        public Guid SubscriptionToken { get; set; }
    }
}
