﻿using System;
using System.Runtime.Serialization;

namespace Messaging
{
    [Serializable]
    public class ReplyGeneratorAlreadyRegisteredException : Exception
    {
        public ReplyGeneratorAlreadyRegisteredException() { }
        public ReplyGeneratorAlreadyRegisteredException(string message) : base(message) { }
        public ReplyGeneratorAlreadyRegisteredException(string message, Exception inner) : base(message, inner) { }
        protected ReplyGeneratorAlreadyRegisteredException(
          SerializationInfo info,
          StreamingContext context)
            : base(info, context) { }
    }

    [Serializable]
    public class MessageHandlingException : Exception
    {
        public MessageHandlingException() { }
        public MessageHandlingException(string message) : base(message) { }
        public MessageHandlingException(string message, Exception inner) : base(message, inner) { }
        protected MessageHandlingException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }

    [Serializable]
    public class InvalidLocalQueueException : Exception
    {
        public InvalidLocalQueueException() { }
        public InvalidLocalQueueException(string message) : base(message) { }
        public InvalidLocalQueueException(string message, Exception inner) : base(message, inner) { }
        protected InvalidLocalQueueException(
          SerializationInfo info,
          StreamingContext context)
            : base(info, context) { }
    }

    [Serializable]
    public class TargetEndpointNotFoundException : Exception
    {
        public TargetEndpointNotFoundException() { }
        public TargetEndpointNotFoundException(string message) : base(message) { }
        public TargetEndpointNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected TargetEndpointNotFoundException(
          SerializationInfo info,
          StreamingContext context)
            : base(info, context) { }
    }
}