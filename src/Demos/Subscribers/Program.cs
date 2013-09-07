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
        static readonly BusEndpoint publisherEndpoint = new BusEndpoint(".", "test_queue");

        static readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();

        static void Main(string[] args)
        {
            const string baseQueueName = "subscriber";
            var tasks = new Task[3];
            for (var i = 0; i < 3; i++)
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
