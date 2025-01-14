using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Models
{
    public class KeyExchangeModel
    {
        public bool NewKeyRequest { get; set; }
        public bool NewKeyResponse { get; set; }
        public bool ForgotKeyRequest { get; set; }
        public string PrivateKey { get; set; }
        public string Username { get; set; }
    }
}
