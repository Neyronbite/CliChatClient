using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Models
{
    public class MessageModel
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Message { get; set; }
    }
}
