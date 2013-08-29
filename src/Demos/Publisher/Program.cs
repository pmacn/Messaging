using Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Messaging.Demos
{
    class Program
    {
        static void Main(string[] args)
        {
            var bus = new ServiceBus("test_queue");
            bus.Start();
            string textMessage;
            while (!String.IsNullOrWhiteSpace((textMessage = Console.ReadLine())))
            {
                var message = new TestMessage { Id = Guid.NewGuid(), Timestamp = DateTime.Now, Message = textMessage };
                bus.Publish(message);
            }
        }
    }
}
