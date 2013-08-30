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
        static CancellationTokenSource _tokenSource = new CancellationTokenSource();

        static void Main(string[] args)
        {
            Task[] listenerTasks = new Task[2];
            for (int i = 0; i < 2; i++)
            {
                var listenerId = i;
                listenerTasks[i] = Task.Run(() => StartListener(listenerId), _tokenSource.Token);
            }

            while (!Console.KeyAvailable)
            {
                Thread.Sleep(10);
            }

            _tokenSource.Cancel();
            Task.WaitAll(listenerTasks);
        }

        private static void StartListener(int listenerId)
        {
            var token = _tokenSource.Token;

            if(token.IsCancellationRequested)
                return;

            using (var bus = new ServiceBus("test_queue"))
            {
                bus.MessageHandlers.Add<TestMessage>(m => Console.WriteLine(String.Format("Listener {0} received \"{1}\"", listenerId, m.Message)));
                bus.Start();

                while (!token.IsCancellationRequested)
                {
                    Thread.Sleep(1);
                }
            }
        }
    }
}