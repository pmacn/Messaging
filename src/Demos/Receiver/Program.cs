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
            using (var bus = new ServiceBus("test_queue"))
            {
                bus.RegisterReplyGenerator<TestMessage>(m => new TestReply(m.Id));
                bus.MessageHandlers.Add<TestMessage>(m => Console.WriteLine("{0} - {1}", m.Id, m.Timestamp));
                bus.Start();
                while (!Console.KeyAvailable)
                {
                    Thread.Sleep(1);
                }
            }
        }
    }
}
