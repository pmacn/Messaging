using System.Messaging;

namespace Messaging
{
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
}