using System;

namespace Messaging.Demos
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var bus = new ServiceBus("my_test_queue"))
            {
                bus.MessageHandlers.Add<TestReply>(r => Console.WriteLine("Got reply to: {0}, Sent on second: {1}", r.ReplyTo, r.SecondSent));
                bus.TargetEndpoints.SetFor<TestMessage>(new BusEndpoint(".", "test_queue"));
                bus.Start();

                var textMessage = Console.ReadLine();
                while (!String.IsNullOrWhiteSpace(textMessage))
                {
                    bus.Send(new SpecialTestMessage { Id = Guid.NewGuid(), Timestamp = DateTime.Now, Message = textMessage, SpecialnessFactor = 3 });
                    textMessage = Console.ReadLine();
                }
            }
        }
    }
}
