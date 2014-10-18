using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CCPrac2.NetChange
{
    class MessageData
    {
        public char messageType;
        public int fromId;
        public string[] data;

        public MessageData(char messageType, int fromId, string[] data)
        {
            this.messageType = messageType;
            this.fromId = fromId;
            this.data = data;
        }
    }
}
