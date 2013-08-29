using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messaging.Demos
{
    [Serializable]
    public class TestReply
    {
        public TestReply(Guid messageId)
        {
            ReplyTo = messageId;
            SecondSent = DateTime.Now.Second;
        }

        public Guid ReplyTo { get; set; }

        public int SecondSent { get; set; }
    }
}

