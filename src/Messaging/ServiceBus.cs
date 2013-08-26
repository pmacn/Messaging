
namespace Messaging
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;

    public interface IServiceBus : IMessageBus
    {
        SubscriberEndpointNotFoundPolicy SubscriberNotFoundPolicy { get; set; }

        /// <summary>
        /// Publishes a message to all subscribers that have registered for the specific type of message.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <param name="message"></param>
        void Publish<TMessage>(TMessage message)
            where TMessage : class;

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

    public enum SubscriberEndpointNotFoundPolicy
    {
        Keep,
        Remove
    }

    public class ServiceBus : IServiceBus
    {
        private readonly List<SubscriptionDescriptor> _subscribers = new List<SubscriptionDescriptor>();

        private readonly List<SubscriptionStarted> _activeSubscriptions = new List<SubscriptionStarted>();

        private readonly Func<IMessageBus> _busFactory;

        private IMessageBus _messageBus;

        public ServiceBus(Func<IMessageBus> busFactory)
        {
            Contract.Requires<ArgumentNullException>(busFactory != null, "busFactory cannot be null");

            _busFactory = busFactory;
            _messageBus = busFactory();
            _messageBus.RegisterMessageHandler<StartSubscriptionCommand>(Handle);
            _messageBus.RegisterMessageHandler<EndSubscriptionCommand>(Handle);
            SubscriberNotFoundPolicy = SubscriberEndpointNotFoundPolicy.Remove;
        }

        public SubscriberEndpointNotFoundPolicy SubscriberNotFoundPolicy { get; set; }

        public void Publish<TMessage>(TMessage message)
            where TMessage : class
        {
            if (!IsRunning)
                throw new InvalidOperationException("Bus has not been started");

            var messageType = typeof(TMessage);
            var targetEndpointsNotFound = new List<SubscriptionDescriptor>();
            foreach (var subscription in _subscribers.Where(s => s.MessageTypeHandle.Equals(messageType.TypeHandle)))
            {
                try
                {
                    _messageBus.Send(message, subscription.Endpoint);
                }
                catch (TargetEndpointNotFoundException)
                {
                    targetEndpointsNotFound.Add(subscription);
                }
            }

            if (SubscriberNotFoundPolicy == SubscriberEndpointNotFoundPolicy.Remove)
                _subscribers.RemoveAll(targetEndpointsNotFound.Contains);
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

        public void Dispose()
        {
            foreach (var sub in _activeSubscriptions)
            {
                _messageBus.Send(new EndSubscriptionCommand { SubscriptionToken = sub.SubscriptionToken }, sub.PublisherEndpoint);
            }
            
            _activeSubscriptions.Clear();

            if (_messageBus != null)
            {
                _messageBus.Dispose();
            }

            _subscribers.Clear();
            _activeSubscriptions.Clear();
        }

        private void Handle(SubscriptionStarted command)
        {
            _activeSubscriptions.Add(command);
        }

        private void Handle(StartSubscriptionCommand command)
        {
            var subscriber = new SubscriptionDescriptor { Endpoint = command.SubscriberEndpoint, MessageTypeHandle = command.MessageTypeHandle, SubscriptionToken = Guid.NewGuid() };
            _subscribers.Add(subscriber);
            SendConfirmation(subscriber);
        }

        private void Handle(EndSubscriptionCommand command)
        {
            _subscribers.RemoveAll(s => s.SubscriptionToken == command.SubscriptionToken);
        }

        private void SendConfirmation(SubscriptionDescriptor subscriber)
        {
            _messageBus.Send(
                new SubscriptionStarted
                {
                    MessageTypeHandle = subscriber.MessageTypeHandle,
                    SubscriptionToken = subscriber.SubscriptionToken,
                    PublisherEndpoint = _messageBus.LocalEndpoint
                },
                subscriber.Endpoint);
        }

        #region IMessageBus pass-through

        public QueueEndpoint LocalEndpoint { get { return _messageBus.LocalEndpoint; } }

        public bool IsRunning { get { return _messageBus.IsRunning; } }

        public void Send<TMessage>(TMessage message) { _messageBus.Send<TMessage>(message); }

        public void Send<TMessage>(TMessage message, QueueEndpoint targetEndpoint)
        { _messageBus.Send<TMessage>(message, targetEndpoint); }

        public void RegisterTargetEndpoint<TMessage>(QueueEndpoint targetEndpoint)
        { _messageBus.RegisterTargetEndpoint<TMessage>(targetEndpoint); }

        public void DeregisterTargetEndpoint<TMessage>(QueueEndpoint targetEndpoint)
        { _messageBus.DeregisterTargetEndpoint<TMessage>(targetEndpoint); }

        public void RegisterMessageHandler<TMessage>(Action<TMessage> handler)
        { _messageBus.RegisterMessageHandler<TMessage>(handler); }

        public void RegisterReplyGenerator<TMessage>(Func<TMessage, object> replyGenerator)
        { _messageBus.RegisterReplyGenerator<TMessage>(replyGenerator); }

        public void Start() { _messageBus.Start(); }

        public void Stop() { _messageBus.Stop(); }
        
        #endregion
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
    public class SubscriptionStarted
    {
        public RuntimeTypeHandle MessageTypeHandle { get; set; }
        public QueueEndpoint PublisherEndpoint { get; set; }
        public Guid SubscriptionToken { get; set; }
    }

    [Serializable]
    public class SubscriptionEnded
    {
        public Guid SubscriptionToken { get; set; }
    }
}
