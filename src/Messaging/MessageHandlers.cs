using System;
using System.Collections.Generic;
using System.Linq;

namespace Messaging
{
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

        internal MessageHandlers() { }

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

        internal IMessageHandler GetFor(Type messageType)
        {
            // should it only get the best fit for the message type?
            // best fit? closest in the inheritance chain?
            // how does it affect subscriptions?
            // how does subscribing to the same message type from two different publishers look?
            // do I need a priority chain when handling messages?
            return _messageHandlers.FirstOrDefault(h => h.MessageType.IsAssignableFrom(messageType));
        }
    }
}