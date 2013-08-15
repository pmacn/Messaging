﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace Messaging
{
    public interface IMessageBus : IDisposable
    {
        /// <summary>
        /// Gets the local queue endpoint
        /// </summary>
        QueueEndpoint LocalEndpoint { get; }

        /// <summary>
        /// Sends a message of type <typeparamref name="TMessage"/> to all
        /// target endpoints registered for that type
        /// </summary>
        /// <typeparam name="TMessage">Type of message to send</typeparam>
        /// <param name="message">Message to send</param>
        void Send<TMessage>(TMessage message);

        /// <summary>
        /// Sends a message to the specified endpoint
        /// </summary>
        /// <typeparam name="TMessage">Type of message to send</typeparam>
        /// <param name="message">The message to send</param>
        /// <param name="targetEndpoint">Endpoint to send the message to</param>
        void Send<TMessage>(TMessage message, QueueEndpoint targetEndpoint);

        /// <summary>
        /// Registers a target endpoint as a recipient of messages of type <typeparamref name="TMessage"/>
        /// </summary>
        /// <typeparam name="TMessage">Type of messages to recieve</typeparam>
        /// <param name="targetEndpoint">Target endpoint that should recieve messages</param>
        void RegisterTargetEndpoint<TMessage>(QueueEndpoint targetEndpoint);

        /// <summary>
        /// Deregisters a target endpoint as no longer being a recipient of message of type <typeparamref name="TMessage"/>
        /// </summary>
        /// <typeparam name="TMessage">Type of messages to no longer recieve</typeparam>
        /// <param name="targetEndpoint">Target endpoint that should no longer recieve messages.</param>
        void DeregisterTargetEndpoint<TMessage>(QueueEndpoint targetEndpoint);

        /// <summary>
        /// Registers a message handler for messages of type <typeparamref name="TMessage"/>
        /// </summary>
        /// <typeparam name="TMessage">Type of messages that this handler can handle</typeparam>
        /// <param name="handler"></param>
        void RegisterMessageHandler<TMessage>(Action<TMessage> handler);

        /// <summary>
        /// Registers a factory that generates replies of type <typeparamref name="TReply"/>
        /// when given a message of type <typeparamref name="TMessage"/>
        /// </summary>
        /// <typeparam name="TMessage">Type of message to reply to</typeparam>
        /// <typeparam name="TReply">Type of reply generated by factory</typeparam>
        /// <param name="replyGenerator"></param>
        void RegisterReplyGenerator<TMessage>(Func<TMessage, object> replyGenerator);

        /// <summary>
        /// Starts listening on the local queue
        /// </summary>
        void Start();

        /// <summary>
        /// Stops listening on the local queue
        /// </summary>
        void Stop();
    }

    public abstract class MessageBus : IMessageBus
    {
        protected readonly TargetEndpoints _targetEndpoints = new TargetEndpoints();

        protected readonly MessageHandlers _messageHandlers = new MessageHandlers();

        protected readonly ReplyGenerator _replyGenerator = new ReplyGenerator();

        public QueueEndpoint LocalEndpoint { get; set; }

        public UnhandledMessagesPolicy UnhandledMessagesPolicy { get; set; }

        public void Send<TMessage>(TMessage message)
        {
            Contract.Requires<ArgumentNullException>(message != null, "message cannot be null");

            foreach(var endpoint in _targetEndpoints.For<TMessage>())
                SendImpl(message, endpoint);
        }

        public void Send<TMessage>(TMessage message, QueueEndpoint targetEndpoint)
        {
            SendImpl(message, targetEndpoint);
        }

        protected abstract void SendImpl<TMessage>(TMessage message, QueueEndpoint endpoint);

        public void RegisterTargetEndpoint<TMessage>(QueueEndpoint targetEndpoint)
        {
            _targetEndpoints.AddFor<TMessage>(targetEndpoint);
        }

        public void DeregisterTargetEndpoint<TMessage>(QueueEndpoint targetEndpoint)
        {
            _targetEndpoints.RemoveFor<TMessage>(targetEndpoint);
        }

        public void RegisterMessageHandler<TMessage>(Action<TMessage> handler)
        {
            _messageHandlers.Add(handler);
        }

        public void DeregisterMessageHandler<TMessage>(Action<TMessage> handler)
        {
            _messageHandlers.Remove(handler);
        }

        public void RegisterReplyGenerator<TMessage>(Func<TMessage, object> replyGenerator)
        {
            _replyGenerator.AddFor<TMessage>(replyGenerator);
        }

        public abstract void Start();

        public abstract void Stop();

        protected void HandleMessage(object message)
        {
            Contract.Requires<ArgumentNullException>(message != null, "message cannot be null");

            try
            {
                HandleMessageImpl((dynamic)message);
            }
            catch
            {

            }
        }

        private void HandleMessageImpl<TMessage>(TMessage message)
        {
            var handlers = _messageHandlers.For<TMessage>();
            if (!handlers.Any())
                return; // TODO: Should be doing something here.

            foreach (var handler in _messageHandlers.For<TMessage>())
            {
                handler(message);
            }
        }

        public abstract void Dispose();

        protected class MessageHandlers
        {
            private readonly ConcurrentDictionary<RuntimeTypeHandle, IList> _handlers =
                new ConcurrentDictionary<RuntimeTypeHandle, IList>();

            public void Add<TMessage>(Action<TMessage> handler)
            {
                GetHandlersFor<TMessage>()
                         .Add(handler);
            }

            public IEnumerable<Action<TMessage>> For<TMessage>()
            {
                return GetHandlersFor<TMessage>().ToList();
            }

            public void Remove<TMessage>(Action<TMessage> handler)
            {
                GetHandlersFor<TMessage>().Remove(handler);
            }

            private List<Action<TMessage>> GetHandlersFor<TMessage>()
            {
                return (List<Action<TMessage>>)_handlers.GetOrAdd(typeof(TMessage).TypeHandle, new List<Action<TMessage>>());
            }
        }

        protected class TargetEndpoints
        {
            private readonly ConcurrentDictionary<RuntimeTypeHandle, List<QueueEndpoint>> _endpoints =
                new ConcurrentDictionary<RuntimeTypeHandle, List<QueueEndpoint>>();

            public IEnumerable<QueueEndpoint> For<TMessage>() { return EndpointsFor<TMessage>().ToList(); }

            public void AddFor<TMessage>(QueueEndpoint endpoint) { EndpointsFor<TMessage>().Add(endpoint); }

            public void RemoveFor<TMessage>(QueueEndpoint endpoint) { EndpointsFor<TMessage>().Remove(endpoint); }

            private List<QueueEndpoint> EndpointsFor<TMessage>() { return _endpoints.GetOrAdd(typeof(TMessage).TypeHandle, new List<QueueEndpoint>()); }
        }

        protected class ReplyGenerator
        {
            private readonly ConcurrentDictionary<RuntimeTypeHandle, Delegate> _replyGenerators =
                new ConcurrentDictionary<RuntimeTypeHandle, Delegate>();

            public void AddFor<TMessage>(Func<TMessage, object> replyGenerator)
            {
                _replyGenerators[typeof(TMessage).TypeHandle] = replyGenerator;
            }

            public object GenerateReplyTo<TMessage>(TMessage message)
            {
                return For<TMessage>().Invoke(message);
            }

            public void RemoveFor<TMessage>()
            {
                Delegate removedObject;
                _replyGenerators.TryRemove(typeof(TMessage).TypeHandle, out removedObject);
            }

            public Func<TMessage, object> For<TMessage>()
            {
                return (Func<TMessage, object>)_replyGenerators[typeof(TMessage).TypeHandle];
            }
        }
    }

    [Serializable]
    public struct QueueEndpoint
    {
        public readonly string MachineName;

        public readonly string QueueName;

        public QueueEndpoint(string machineName, string queueName)
        {
            MachineName =
                machineName.IsLocalhost() ?
                Environment.MachineName :
                machineName;

            QueueName = queueName;
        }
    }

    internal static class StringExtension
    {
        public static bool IsLocalhost(this string machineName)
        {
            return String.IsNullOrWhiteSpace(machineName) ||
                   machineName.Equals(".", StringComparison.InvariantCultureIgnoreCase) ||
                   machineName.Equals("localhost", StringComparison.InvariantCultureIgnoreCase);
        }
    }

    public enum UnhandledMessagesPolicy
    {
        Leave = 1,
        Requeue = 2,
        LogError = 3,
        Discard = 4
    }
}
