using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Models
{
    public class GroupKeyExchangeModel
    {
        public string OwnerUsername { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string GroupId { get; set; }
        public bool ServerRequest { get; set; }
        public bool ExchangeRequest { get; set; }
        public bool ExchangeResponse { get; set; }
        public bool ForgotKeyRequest { get; set; }
        public string PrivateKey { get; set; }
    }
}
