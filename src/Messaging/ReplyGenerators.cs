using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;

namespace Messaging
{
    internal class ReplyGenerators
    {
        private readonly ConcurrentDictionary<Type, Delegate> _replyGenerators = new ConcurrentDictionary<Type, Delegate>();

        public void AddFor<TMessage>(Func<TMessage, object> replyGenerator)
        {
            var messageType = typeof(TMessage);
            _replyGenerators.AddOrUpdate(messageType, replyGenerator, (t, d) => replyGenerator);
        }

        public object GenerateReplyTo(object message)
        {
            var messageType = message.GetType();
            Delegate generator;
            if (!_replyGenerators.TryGetValue(messageType, out generator))
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
}

