using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messaging
{
    public interface IMessageBusFactory
    {
        IMessageBus CreateBus(QueueEndpoint localEndpoint);
    }

    public class MsmqMessageBusFactory : IMessageBusFactory
    {
        public IMessageBus CreateBus(QueueEndpoint localEndpoint)
        {
            return new MsmqMessageBus(localEndpoint.QueueName);
        }
    }

}
