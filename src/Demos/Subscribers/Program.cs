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
        static BusEndpoint publisherEndpoint = new BusEndpoint("5ZB00R1", "test_queue");

        static CancellationTokenSource _tokenSource = new CancellationTokenSource();

        static void Main(string[] args)
        {
            var baseQueueName = "subscriber";
            var tasks = new Task[3];
            for (int i = 0; i < 3; i++)
            {
                var queueName = baseQueueName + i;
                tasks[i] = Task.Run(() => StartSubscriber(queueName));
            }

            while (!Console.KeyAvailable)
            {
                Thread.Sleep(10);
            }

            _tokenSource.Cancel();
            Task.WaitAll(tasks);
        }

        private static void TestMessageHandler(TestMessage m) { Console.WriteLine("{0} : {1} ", m.Timestamp, m.Message); }

        private static void StartSubscriber(string queueName)
        {
            using (var bus = new ServiceBus(queueName))
            {
                bus.Start();
                bus.SubscribeTo<TestMessage>(publisherEndpoint, TestMessageHandler);

                while (!_tokenSource.Token.IsCancellationRequested)
                {
                    Thread.Sleep(1);
                }

                bus.Unsubscribe<TestMessage>(publisherEndpoint);
            }
        }
    }
}
