
namespace Messaging
{
    using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
    using System.Linq.Expressions;

    public interface IServiceBus : IMessageBus
    {
        SubscriberEndpointNotFoundPolicy SubscriberNotFoundPolicy { get; set; }

        /// <summary>
        /// Publishes a message to all subscribers that have registered for the specific type of message.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <param name="message"></param>
        void Publish(object message);

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

    public abstract class AbstractServiceBus : IServiceBus
    {
        private readonly TargetEndpoints _targetEndpoints = new TargetEndpoints();

        private readonly Subscribers _subscribers = new Subscribers();

        private readonly List<SubscriptionStarted> _subscriptions = new List<SubscriptionStarted>();

        private readonly ReplyGenerators _replyGenerators = new ReplyGenerators();

        protected AbstractServiceBus()
        {
            MessageHandlers = new MessageHandlers();
            MessageHandlers.Add<StartSubscriptionRequest>(Handle);
            MessageHandlers.Add<EndSubscriptionRequest>(Handle);
        }

        private void Handle(EndSubscriptionRequest request)
        {
            _subscribers.Remove(request);
        }

        private void Handle(StartSubscriptionRequest request)
        {
            _subscribers.Add(request);
        }

        public MessageHandlers MessageHandlers { get; private set; }

        public SubscriberEndpointNotFoundPolicy SubscriberNotFoundPolicy { get; set; }

        public void Publish(object message)
        {
            var messageType = message.GetType();
            foreach (var subscriber in _subscribers.GetFor(messageType))
            {
                SendImpl(message, subscriber);
            }
        }

        public void SubscribeTo<TMessage>(QueueEndpoint publisherEndpoint, Action<TMessage> handler) where TMessage : class
        {
            MessageHandlers.Add<TMessage>(handler);
            Start();
            var request = new StartSubscriptionRequest { Token = Guid.NewGuid(), MessageType = typeof(TMessage), SubscriberEndpoint = LocalEndpoint };
            SendImpl(request, publisherEndpoint);
        }

        public void Unsubscribe<TMessage>(QueueEndpoint publisherEndpoint)
            where TMessage : class
        {
            var messageType = typeof(TMessage);
            var subsToEnd = _subscriptions.Where(s => s.MessageType == messageType && s.PublisherEndpoint == publisherEndpoint).ToList();
            foreach (var sub in subsToEnd)
            {
                SendImpl(new EndSubscriptionRequest { SubscriptionToken = sub.SubscriptionToken }, sub.PublisherEndpoint);
            }

            _subscriptions.RemoveAll(subsToEnd.Contains);
        }

        public QueueEndpoint LocalEndpoint { get; protected set; }

        public bool IsRunning { get; protected set; }

        public void Send(object message)
        {
            var messageType = message.GetType();
            foreach (var endpoint in _targetEndpoints.For(messageType))
            {
                SendImpl(message, endpoint);
            }
        }

        public void Send(object message, QueueEndpoint targetEndpoint)
        {
            SendImpl(message, targetEndpoint);
        }

        protected abstract void SendImpl(object message, QueueEndpoint targetEndpoint);

        protected void HandleMessage(object message)
        {
            var messageType = message.GetType();
            foreach (var handler in MessageHandlers.For(messageType))
            {
                handler.Handle(message);
            }
        }

        protected void SendReplies(object message, QueueEndpoint responseEndpoint)
        {
            var reply = _replyGenerators.GenerateReplyTo(message);
            if(reply != null)
                SendImpl(reply, responseEndpoint);
        }

        public void RegisterTargetEndpoint<TMessage>(QueueEndpoint targetEndpoint)
        {
            _targetEndpoints.AddFor<TMessage>(targetEndpoint);
        }

        public void DeregisterTargetEndpoint<TMessage>(QueueEndpoint targetEndpoint)
        {
            _targetEndpoints.RemoveFor<TMessage>(targetEndpoint);
        }

        public void RegisterReplyGenerator<TMessage>(Func<TMessage, object> replyGenerator)
        {
            
        }

        public void Start()
        {
            if(IsRunning)
                return;

            StartImpl();
            IsRunning = true;
        }

        protected abstract void StartImpl();

        public void Stop()
        {
            if(!IsRunning)
                return;

            IsRunning = false;
            StopImpl();
        }

        protected abstract void StopImpl();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) { }
    }

    internal class Subscribers
    {
        List<SubscriberDescriptor> _subscribers = new List<SubscriberDescriptor>();

        public IEnumerable<QueueEndpoint> GetFor(Type messageType)
        {
            lock (_subscribers)
            {
                return _subscribers.Where(s => s.MessageType.IsAssignableFrom(messageType))
                                   .Select(s => s.Endpoint)
                                   .ToList();
            }
        }

        public void Add(StartSubscriptionRequest request)
        {
            var desc = new SubscriberDescriptor { MessageType = request.MessageType, Endpoint = request.SubscriberEndpoint, SubscriptionToken = request.Token };
            lock (_subscribers)
            {
                _subscribers.Add(desc);
            }
        }

        internal void Remove(EndSubscriptionRequest request)
        {
            lock (_subscribers)
            {
                _subscribers.RemoveAll(s => s.SubscriptionToken == request.SubscriptionToken);
            }
        }
    }

    internal class TargetEndpoints
    {
        List<TargetEndpoint> _endpoints = new List<TargetEndpoint>();

        public void AddFor<TMessage>(QueueEndpoint endpoint)
        {
            var messageType = typeof(TMessage);
            lock (_endpoints)
            {
                if (!_endpoints.Any(e => e.MessageType.IsAssignableFrom(messageType) && e.Endpoint == endpoint))
                {
                    _endpoints.Add(new TargetEndpoint { MessageType = messageType, Endpoint = endpoint });
                }
            }
        }

        public void RemoveFor<TMessage>(QueueEndpoint endpoint)
        {
            var messageType = typeof(TMessage);
            lock (_endpoints)
            {
                _endpoints.RemoveAll(e => e.MessageType == messageType && e.Endpoint == endpoint);
            }
        }

        public IEnumerable<QueueEndpoint> For(Type messageType)
        {
            return _endpoints.Where(e => e.MessageType.IsAssignableFrom(messageType))
                             .Select(e => e.Endpoint)
                             .ToList();
        }
    }

    internal class TargetEndpoint
    {
        public Type MessageType { get; set; }
        public QueueEndpoint Endpoint { get; set; }
    }

    internal interface IMessageHandler
    {
        Type MessageType { get; }

        void Handle(object message);
    }

    internal class MessageHandler<TMessage> : IMessageHandler
    {
        private Action<TMessage> _handler;

        public MessageHandler(Action<TMessage> handler)
        {
            MessageType = typeof(TMessage);
            _handler = handler;
        }

        public Type MessageType { get; private set; }

        public void Handle(object message)
        {
            _handler((TMessage)message);
        }
    }

    public class MessageHandlers
    {
        private List<IMessageHandler> _messageHandlers = new List<IMessageHandler>();

        internal MessageHandlers()
        {
        }

        public void Add<TMessage>(Action<TMessage> handler)
        {
            var messageType = typeof(TMessage);
            // TODO: switch this around so we're not creating an instance if we don't have to
            var messageHandler = new MessageHandler<TMessage>(handler);
            lock (_messageHandlers)
            {
                if (!_messageHandlers.Contains(messageHandler))
                    _messageHandlers.Add(messageHandler);
            }
        }

        public void Remove<TMessage>(Action<TMessage> handler)
        {
            var messageType = typeof(TMessage);
            lock (_messageHandlers)
            {
                _messageHandlers.RemoveAll(h => h.MessageType == messageType);
            }
        }

        internal IEnumerable<IMessageHandler> For(Type messageType)
        {
            return _messageHandlers.Where(h => messageType.IsAssignableFrom(h.MessageType))
                                   .ToList();
        }
    }

    internal class ReplyGenerators
    {
        private readonly ConcurrentDictionary<Type, Delegate> _replyGenerators =
            new ConcurrentDictionary<Type, Delegate>();

        public void AddFor<TMessage>(Func<TMessage, object> replyGenerator)
        {
            var messageType = typeof(TMessage);
            _replyGenerators.AddOrUpdate(messageType, replyGenerator, (t, d) => replyGenerator);
        }

        public object GenerateReplyTo(object message)
        {
            var messageType = message.GetType();

            Delegate generator;
            if(!_replyGenerators.TryGetValue(messageType, out generator))
                return null;

            return generator.DynamicInvoke(message);
        }

        public void RemoveFor<TMessage>()
        {
            var messageType = typeof(TMessage);
            Delegate removedGenerator;
            _replyGenerators.TryRemove(messageType, out removedGenerator);
        }
    }

    public enum SubscriberEndpointNotFoundPolicy
    {
        Keep,
        Remove
    }

    //public abstract class ServiceBus : IServiceBus
    //{
    //    private readonly List<SubscriberDescriptor> _subscribers = new List<SubscriberDescriptor>();

    //    private readonly List<SubscriptionStarted> _subscriptions = new List<SubscriptionStarted>();

    //    private readonly List<Subscription> Subscriptions = new List<Subscription>();

    //    private readonly Func<IMessageBus> _busFactory;

    //    private readonly IMessageBus _messageBus;

    //    protected ServiceBus(Func<IMessageBus> busFactory)
    //    {
    //        Contract.Requires<ArgumentNullException>(busFactory != null, "busFactory cannot be null");

    //        _busFactory = busFactory;
    //        _messageBus = busFactory();
    //        _messageBus.RegisterMessageHandler<StartSubscriptionCommand>(Handle);
    //        _messageBus.RegisterMessageHandler<EndSubscriptionCommand>(Handle);
    //        SubscriberNotFoundPolicy = SubscriberEndpointNotFoundPolicy.Remove;
    //    }

    //    public SubscriberEndpointNotFoundPolicy SubscriberNotFoundPolicy { get; set; }

    //    public void Publish<TMessage>(TMessage message)
    //        where TMessage : class
    //    {
    //        if (!IsRunning)
    //            throw new InvalidOperationException("Bus has not been started");

    //        var messageType = typeof(TMessage);
    //        var targetEndpointsNotFound = new List<SubscriberDescriptor>();
    //        foreach (var subscription in _subscribers.Where(s => s.MessageType == messageType).ToList())
    //        {
    //            try
    //            {
    //                _messageBus.Send(message, subscription.Endpoint);
    //            }
    //            catch (TargetEndpointNotFoundException)
    //            {
    //                targetEndpointsNotFound.Add(subscription);
    //            }
    //        }

    //        if (SubscriberNotFoundPolicy == SubscriberEndpointNotFoundPolicy.Remove)
    //            _subscribers.RemoveAll(targetEndpointsNotFound.Contains);
    //    }

    //    public void SubscribeTo<TMessage>(QueueEndpoint publisherEndpoint, Action<TMessage> handler)
    //        where TMessage : class
    //    {
    //        _messageBus.Start();
    //        _messageBus.RegisterMessageHandler<TMessage>(handler);
    //        var subCommand = new StartSubscriptionCommand { MessageType = typeof(TMessage), SubscriberEndpoint = _messageBus.LocalEndpoint };
    //        _messageBus.Send(subCommand, publisherEndpoint);
    //    }

    //    public void Unsubscribe<TMessage>(QueueEndpoint publisherEndpoint)
    //        where TMessage : class
    //    {
    //        var messageType = typeof(TMessage);
    //        var subscription = _subscriptions.SingleOrDefault(s => s.MessageType == messageType && s.PublisherEndpoint == publisherEndpoint);
    //        if (subscription == null)
    //            return;

    //        var subCommand = new EndSubscriptionCommand { SubscriptionToken = subscription.SubscriptionToken };
    //        _messageBus.Send(subCommand, publisherEndpoint);
    //        _subscriptions.Remove(subscription);
    //    }

    //    public void Dispose()
    //    {
    //        foreach (var sub in _subscriptions)
    //        {
    //            _messageBus.Send(new EndSubscriptionCommand { SubscriptionToken = sub.SubscriptionToken }, sub.PublisherEndpoint);
    //        }
            
    //        _subscriptions.Clear();

    //        if (_messageBus != null)
    //        {
    //            _messageBus.Dispose();
    //        }

    //        _subscribers.Clear();
    //        _subscriptions.Clear();
    //    }

    //    private void Handle(SubscriptionStarted command)
    //    {
    //        _subscriptions.Add(command);
    //        Subscriptions.Add(new Subscription(command.SubscriptionToken));
    //    }

    //    private void Handle(StartSubscriptionCommand command)
    //    {
    //        var subscriber = new SubscriberDescriptor { Endpoint = command.SubscriberEndpoint, MessageType = command.MessageType, SubscriptionToken = Guid.NewGuid() };
    //        _subscribers.Add(subscriber);
    //        SendConfirmation(subscriber);
    //    }

    //    private void Handle(EndSubscriptionCommand command)
    //    {
    //        _subscribers.RemoveAll(s => s.SubscriptionToken == command.SubscriptionToken);
    //    }

    //    private void SendConfirmation(SubscriberDescriptor subscriber)
    //    {
    //        _messageBus.Send(
    //            new SubscriptionStarted
    //            {
    //                MessageType = subscriber.MessageType,
    //                SubscriptionToken = subscriber.SubscriptionToken,
    //                PublisherEndpoint = _messageBus.LocalEndpoint
    //            },
    //            subscriber.Endpoint);
    //    }

    //    #region IMessageBus pass-through

    //    public QueueEndpoint LocalEndpoint { get { return _messageBus.LocalEndpoint; } }

    //    public bool IsRunning { get { return _messageBus.IsRunning; } }

    //    public void Send<TMessage>(TMessage message) { _messageBus.Send<TMessage>(message); }

    //    public void Send<TMessage>(TMessage message, QueueEndpoint targetEndpoint)
    //    { _messageBus.Send<TMessage>(message, targetEndpoint); }

    //    public void RegisterTargetEndpoint<TMessage>(QueueEndpoint targetEndpoint)
    //    { _messageBus.RegisterTargetEndpoint<TMessage>(targetEndpoint); }

    //    public void DeregisterTargetEndpoint<TMessage>(QueueEndpoint targetEndpoint)
    //    { _messageBus.DeregisterTargetEndpoint<TMessage>(targetEndpoint); }

    //    public void RegisterMessageHandler<TMessage>(Action<TMessage> handler)
    //    { _messageBus.RegisterMessageHandler<TMessage>(handler); }

    //    public void RegisterReplyGenerator<TMessage>(Func<TMessage, object> replyGenerator)
    //    { _messageBus.RegisterReplyGenerator<TMessage>(replyGenerator); }

    //    public void Start() { _messageBus.Start(); }

    //    public void Stop() { _messageBus.Stop(); }
        
    //    #endregion
    //}

    public class SubscriberDescriptor
    {
        public Type MessageType { get; set; }
        public QueueEndpoint Endpoint { get; set; }
        public Guid SubscriptionToken { get; set; }
    }

    public class Subscription
    {
        private Guid _token;

        internal Subscription (Guid token)
        {
            _token = token;
        }

        internal Guid GetToken()
        {
            return _token;
        }
    }

    [Serializable]
    public class StartSubscriptionRequest
    {
        public Guid Token { get; set; }

        public Type MessageType { get; set; }

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
    public class EndSubscriptionRequest
    {
        public Guid SubscriptionToken { get; set; }
    }

    [Serializable]
    public class SubscriptionStarted
    {
        public Type MessageType { get; set; }
        public QueueEndpoint PublisherEndpoint { get; set; }
        public Guid SubscriptionToken { get; set; }
    }

    [Serializable]
    public class SubscriptionEnded
    {
        public Guid SubscriptionToken { get; set; }
    }
}
