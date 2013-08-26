using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Messaging;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Transactions;

namespace Messaging
{
    public class MsmqMessageBus : MessageBus
    {
        private MessageQueue _localQueue;

        private IMessageFormatter _messageFormatter = new BinaryMessageFormatter();

        public MsmqMessageBus(string localQueue)
            : base(new QueueEndpoint("", localQueue))
        { }

        protected override void SendImpl<TMessage>(TMessage message, QueueEndpoint endpoint)
        {
            var endpointName = MsmqEndpointParser.GetEndpointName(endpoint);
            if (!MessageQueue.Exists(endpointName))
                throw new TargetEndpointNotFoundException(String.Format("Unable to find target endpoint {0}@{1}", endpoint.QueueName, endpoint.MachineName));

            using (var targetQueue = new MessageQueue(endpointName))
            {
                targetQueue.SendMessage(WrapInEnvelope(message));
            }
        }

        protected override void StartImpl()
        {
            SetupLocalQueue();
            StartListening();
        }

        protected override void StopImpl()
        {
            if (_localQueue == null)
                return;

            _localQueue.PeekCompleted -= PeekCompletedHandler;
            _localQueue.ReceiveCompleted -= ReceiveCompletedHandler;
            _localQueue.Dispose();
            _localQueue = null;
        }

        private Message WrapInEnvelope<TMessage>(TMessage body)
        {
            return new Message {
                Body = body,
                Label = typeof(TMessage).FullName,
                Recoverable = true,
                Formatter = _messageFormatter,
                ResponseQueue = _localQueue
            };
        }

        private void SetupLocalQueue()
        {
            var queuePath = MsmqEndpointParser.GetEndpointName(LocalEndpoint);
            _localQueue = GetOrCreateMessageQueue(queuePath);
            _localQueue.Formatter = _messageFormatter;
        }

        private void StartListening()
        {
            if (!_localQueue.CanRead)
                throw new NotSupportedException("Unable to read from local queue.");

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

        private MessageQueue GetOrCreateMessageQueue(string endpoint)
        {
            return MessageQueue.Exists(endpoint) ?
                   new MessageQueue(endpoint) :
                   MessageQueue.Create(endpoint, true);
        }

        private void ReceiveCompletedHandler(object sender, ReceiveCompletedEventArgs e)
        {
            var receptionQueue = (MessageQueue)sender;
            var queueMessage = receptionQueue.EndReceive(e.AsyncResult);
            ProcessMessage(queueMessage);
            receptionQueue.BeginReceive();
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

        private void ProcessMessage(Message queueMessage)
        {
            var messageBody = queueMessage.Body;
            if (messageBody == null)
                throw new Exception(String.Format("Unable to extract message. Label: {0}", queueMessage.Label));

            HandleMessage(messageBody);
            SendReplies(queueMessage);
        }

        private void SendReplies(Message message)
        {
            if (message == null || message.ResponseQueue == null)
                return;

            var reply = (object)_replyGenerator.GenerateReplyTo((dynamic)message.Body);
            if(reply != null)
                message.ResponseQueue.SendMessage(WrapInEnvelope(reply));
        }

        private void SendToErrorQueue(Message queueMessage)
        {
            var errorQueuePath = MsmqEndpointParser.GetErrorQueuePath(LocalEndpoint);
            using (var mq = GetOrCreateMessageQueue(errorQueuePath))
            {
                mq.SendMessage(queueMessage);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                Stop();
            }
        }
    }

    internal static class MessageQueueExtensions
    {
        public static void SendMessage(this MessageQueue mq, Message message)
        {
            if (mq.Transactional)
            {
                using (var transaction = new MessageQueueTransaction())
                {
                    transaction.Begin();
                    mq.Send(message, transaction);
                    transaction.Commit();
                }
            }
            else
            {
                mq.Send(message);
            }
        }
    }

    internal static class MsmqEndpointParser
    {
        public static string GetLocalQueuePath(QueueEndpoint endpoint)
        {
            if(IsLocalhost(endpoint.MachineName))
                throw new InvalidLocalQueueException("MSMQ can only listen to local queues");

            return String.Format(@".\private$\{0}", endpoint.QueueName);
        }

        public static string GetEndpointName(QueueEndpoint endpoint)
        {
            if (String.IsNullOrWhiteSpace(endpoint.QueueName))
                throw new InvalidOperationException("Queue name cannot be null or empty");

            if (IsLocalhost(endpoint.MachineName))
                return string.Format(@".\private$\{0}", endpoint.QueueName);

            return String.Format(@"{0}\private$\{1}", endpoint.MachineName, endpoint.QueueName);
        }

        public static string GetErrorQueuePath(QueueEndpoint endpoint) { return GetLocalQueuePath(endpoint) + "_errors"; }

        private static bool IsLocalhost(string machineName)
        {
            if (String.IsNullOrWhiteSpace(machineName))
                return true;

            return machineName.Equals("localhost", StringComparison.OrdinalIgnoreCase) || machineName.Equals(".", StringComparison.OrdinalIgnoreCase);
        }
    }
}