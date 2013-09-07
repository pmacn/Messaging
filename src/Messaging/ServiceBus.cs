using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;

namespace Messaging
{
    /// <summary>
    /// Unneccessary interface ftw!
    /// </summary>
    public interface IServiceBus : IDisposable
    {
        SubscriberNotFoundPolicy SubscriberNotFoundPolicy { get; set; }

        UnhandledMessagesPolicy UnhandledMessagesPolicy { get; set; }

        /// <summary>
        /// Gets the local queue endpoint
        /// </summary>
        BusEndpoint LocalEndpoint { get; }

        /// <summary>
        /// True if the messagebus is currently listening for messages. Otherwise false.
        /// </summary>
        bool IsRunning { get; }

        MessageHandlers MessageHandlers { get; }

        TargetEndpoints TargetEndpoints { get; }

        void RegisterReplyGenerator<TMessage>(Func<TMessage, object> replyGenerator);

        /// <summary>
        /// Sends a message to all target endpoints registered for that type of message.
        /// </summary>
        /// <param name="message">Message to send</param>
        void Send(object message);

        /// <summary>
        /// Starts listening on the local queue
        /// </summary>
        void Start();

        /// <summary>
        /// Stops listening on the local queue
        /// </summary>
        void Stop();

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
        /// <param name="publisherEndpoint"></param>
        /// <param name="handler"></param>
        void SubscribeTo<TMessage>(BusEndpoint publisherEndpoint, Action<TMessage> handler, bool UnsubscribeOnStop = true)
            where TMessage : class;

        /// <summary>
        /// Sends a command to the specified endpoint requesting to stop receiving messages of the specified type.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <param name="publisherEndpoint"></param>
        void Unsubscribe<TMessage>(BusEndpoint publisherEndpoint)
            where TMessage : class;
    }

    [Serializable]
    public class Subscription : IEquatable<Subscription>, IEquatable<Subscriber>
    {
        public Subscription(Type messageType, BusEndpoint subscriberEndpoint, BusEndpoint publisherEndpoint, bool unsubscribeOnStop)
        {
            Token = Guid.NewGuid();
            MessageType = messageType;
            SubscriberEndpoint = subscriberEndpoint;
            PublisherEndpoint = publisherEndpoint;
            UnsubscribeOnStop = unsubscribeOnStop;
        }

        public Guid Token { get; private set; }

        public Type MessageType { get; private set; }

        public BusEndpoint SubscriberEndpoint { get; private set; }

        public BusEndpoint PublisherEndpoint { get; private set; }

        public bool UnsubscribeOnStop { get; private set; }

        public override int GetHashCode()
        {
            return Token.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj is Subscription)
                return Equals((Subscription)obj);
            if (obj is Subscriber)
                return Equals((Subscriber)obj);
            return false;
        }

        public bool Equals(Subscription other)
        {
            if(other == null)
                return false;

            return this.Token == other.Token;
        }

        public bool Equals(Subscriber other)
        {
            if(other == null)
                return false;

            return this.Token == other.Token;
        }
    }

    [Serializable]
    public class Subscriber : IEquatable<Subscriber>, IEquatable<Subscription>
    {
        private Subscriber(Guid token, Type messageType, BusEndpoint endpoint)
        {
            Token = token;
            MessageType = messageType;
            Endpoint = endpoint;
        }

        public Guid Token { get; private set; }

        public Type MessageType { get; private set; }

        public BusEndpoint Endpoint { get; private set; }

        public override int GetHashCode()
        {
            return Token.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if(ReferenceEquals(obj, null))
                return false;
            if(ReferenceEquals(this, obj))
                return true;
            if(obj is Subscriber)
                return Equals((Subscriber)obj);
            if(obj is Subscription)
                return Equals((Subscription)obj);
            return false;
        }

        public bool Equals(Subscriber other)
        {
            if (other == null)
                return false;
            return this.Token == other.Token;
        }

        public bool Equals(Subscription other)
        {
            if (other == null)
                return false;
            return this.Token == other.Token;
        }

        public static Subscriber From(Subscription subscription)
        {
            return new Subscriber(subscription.Token, subscription.MessageType, subscription.SubscriberEndpoint);
        }
    }

    public enum SubscriberNotFoundPolicy
    {
        Keep,
        Remove
    }

    public enum UnhandledMessagesPolicy
    {
        Requeue,
        SendToErrorQueue,
        Discard
    }

    public sealed class ServiceBus : IServiceBus
    {
        private readonly IMessageFormatter _messageFormatter = new BinaryMessageFormatter();

        private readonly List<Subscriber> _subscribers = new List<Subscriber>();

        private readonly Subscriptions _subscriptions = new Subscriptions();

        private readonly ReplyGenerators _replyGenerators = new ReplyGenerators();

        private MessageQueue _localQueue;

        public ServiceBus(string localQueueName)
        {
            LocalEndpoint = new BusEndpoint("", localQueueName);
            MessageHandlers = new MessageHandlers();
            TargetEndpoints = new TargetEndpoints();
            MessageHandlers.Add<StartSubscriptionRequest>(Handle);
            MessageHandlers.Add<EndSubscriptionRequest>(Handle);
            // TODO: Should it be possible to reject a subscription request?
            //MessageHandlers.Add<SubscriptionStarted>(Handle);
            //MessageHandlers.Add<SubscriptionEnded>(Handle);
            _replyGenerators.AddFor<StartSubscriptionRequest>(r => new SubscriptionStarted(r.Subscription));
            _replyGenerators.AddFor<EndSubscriptionRequest>(r => new SubscriptionEnded(r.Subscription));
        }

        private void Handle(StartSubscriptionRequest request)
        {
            _subscribers.Add(Subscriber.From(request.Subscription));
        }

        private void Handle(EndSubscriptionRequest request)
        {
            _subscribers.Remove(Subscriber.From(request.Subscription));
        }

        //private void Handle(SubscriptionStarted obj) { _subscriptions.Add(obj.Subscription); }

        //private void Handle(SubscriptionEnded obj) { _subscriptions.Remove(obj.Subscription); }

        public SubscriberNotFoundPolicy SubscriberNotFoundPolicy { get; set; }

        public UnhandledMessagesPolicy UnhandledMessagesPolicy { get; set; }

        public BusEndpoint LocalEndpoint { get; private set; }

        public bool IsRunning { get; private set; }

        public MessageHandlers MessageHandlers { get; private set; }

        public TargetEndpoints TargetEndpoints { get; private set; }

        public void RegisterReplyGenerator<TMessage>(Func<TMessage, object> replyGenerator)
        {
            _replyGenerators.AddFor<TMessage>(replyGenerator);
        }

        public void Send(object message)
        {
            var messageType = message.GetType();
            foreach(var endpoint in TargetEndpoints.For(messageType))
                Send(message, endpoint);
        }

        private void Send(object message, BusEndpoint targetEndpoint)
        {
            var queuePath = MsmqEndpointParser.GetQueuePath(targetEndpoint);
            if (!MessageQueue.Exists(queuePath))
                throw new TargetEndpointNotFoundException("Unable to reach target endpoint " + targetEndpoint.QueueName + "@" + targetEndpoint.MachineName);
            var remoteQueue = new MessageQueue(queuePath, QueueAccessMode.Send);
            if (!remoteQueue.CanWrite)
                throw new UnableToSendMessageException("Unable to send message to " + targetEndpoint.QueueName + "@" + targetEndpoint.MachineName);
            remoteQueue.SendMessage(WrapInMsmqMessage(message));
        }

        private Message WrapInMsmqMessage(object message)
        {
            return new Message
            {
                Body = message,
                Label = message.GetType().FullName,
                Formatter = _messageFormatter,
                Recoverable = true,
                ResponseQueue = _localQueue
            };
        }

        public void Start()
        {
            if (IsRunning)
                return;
            IsRunning = true;
            var queuePath = MsmqEndpointParser.GetLocalQueuePath(LocalEndpoint);
            _localQueue = GetOrCreateMessageQueue(queuePath);
            _localQueue.Formatter = _messageFormatter;
            StartListening();
        }

        private MessageQueue GetOrCreateMessageQueue(string queuePath)
        {
            try
            {
                return MessageQueue.Exists(queuePath) ?
                       new MessageQueue(queuePath) :
                       MessageQueue.Create(queuePath, true);
            }
            catch (ArgumentException ex)
            {
                throw new UnableToStartMessageBusException(String.Format("{0} is not a valid queue", queuePath), ex);
            }
            catch (MessageQueueException ex)
            {
                throw new UnableToStartMessageBusException("Unable to start message bus", ex);
            }
        }

        private void StartListening()
        {
            if (!_localQueue.CanRead)
                throw new UnableToReadMessagesException(String.Format("Cannot read messages from {0}@{1}", LocalEndpoint.QueueName, LocalEndpoint.MachineName));
            if (_localQueue.Transactional)
            {
                _localQueue.PeekCompleted += PeekCompletedHandler;
                _localQueue.BeginPeek();
            }
            else
            {
                _localQueue.ReceiveCompleted += ReceiveCompletedHandler;
                _localQueue.BeginReceive();
            }
        }

        private void PeekCompletedHandler(object sender, PeekCompletedEventArgs e)
        {
            var queue = (MessageQueue)sender;
            Message queueMessage = null;
            try
            {
                queue.EndPeek(e.AsyncResult);
                queueMessage = queue.Receive();
                ProcessMessage(queueMessage);
            }
            catch (Exception)
            {
                if (queueMessage != null)
                    SendToErrorQueue(queueMessage);
            }
            queue.BeginPeek();
        }

        private void ReceiveCompletedHandler(object sender, ReceiveCompletedEventArgs e)
        {
            var queue = (MessageQueue)sender;
            Message message = null;
            try
            {
                message = queue.EndReceive(e.AsyncResult);
                ProcessMessage(message);
            }
            catch
            {
                if (message != null)
                    SendToErrorQueue(message);
            }
            queue.BeginReceive();
        }

        private void ProcessMessage(Message obj)
        {
            var message = obj.Body;
            var messageType = message.GetType();
            var handler = MessageHandlers.GetFor(messageType);
            if (handler == null)
            {
                switch (UnhandledMessagesPolicy)
                {
                    case UnhandledMessagesPolicy.Requeue:
                        _localQueue.Send(obj);
                        break;
                    case UnhandledMessagesPolicy.SendToErrorQueue:
                        SendToErrorQueue(obj);
                        break;
                    default:
                        break;
                }

                return;
            }
            
            handler.Handle(message);
            SendReply(obj);
        }

        private void SendToErrorQueue(Message obj)
        {
            var errorQueuePath = MsmqEndpointParser.GetErrorQueuePath(LocalEndpoint);
            var errorQueue = GetOrCreateMessageQueue(errorQueuePath);
            errorQueue.Send(obj);
            errorQueue.Dispose();
        }

        private void SendReply(Message msmqMessage)
        {
            if (msmqMessage.ResponseQueue == null)
                return;
            var responseEndpoint = MsmqEndpointParser.GetEndpoint(msmqMessage.ResponseQueue);
            var reply = _replyGenerators.GenerateReplyTo(msmqMessage.Body);
            if (reply != null)
                Send(reply, responseEndpoint);
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            if (_localQueue != null)
            {
                _localQueue.PeekCompleted -= PeekCompletedHandler;
                _localQueue.ReceiveCompleted -= ReceiveCompletedHandler;
                _localQueue.Dispose();
                _localQueue = null;
            }
        }

        public void Publish(object message)
        {
            var messageType = message.GetType();
            var unreachableSubscribers = new List<Subscriber>();
            foreach (var subscriber in _subscribers.Where(s => s.MessageType.IsAssignableFrom(messageType)))
            {
                try
                {
                    Send(message, subscriber.Endpoint);
                }
                catch (TargetEndpointNotFoundException)
                {
                    unreachableSubscribers.Add(subscriber);
                }
            }
            if (SubscriberNotFoundPolicy == SubscriberNotFoundPolicy.Remove)
                _subscribers.RemoveAll(unreachableSubscribers.Contains);
        }

        public void SubscribeTo<TMessage>(BusEndpoint publisherEndpoint, Action<TMessage> handler, bool unsubscribeOnStop = true)
            where TMessage : class
        {
            MessageHandlers.Add<TMessage>(handler);
            var subscription = new Subscription(typeof(TMessage), LocalEndpoint, publisherEndpoint, unsubscribeOnStop);
            _subscriptions.Add(subscription);
            Send(new StartSubscriptionRequest(subscription), publisherEndpoint);
        }

        public void Unsubscribe<TMessage>(BusEndpoint publisherEndpoint)
            where TMessage : class
        {
            var messageType = typeof(TMessage);
            var subs = _subscriptions.For(messageType);
            
            foreach (var subscription in subs)
            {
                Send(new EndSubscriptionRequest(subscription), publisherEndpoint);
            }

            // TODO: Need to also remove the messagehandlers, store them separately from the regular ones?
            _subscriptions.Remove(messageType, publisherEndpoint);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}