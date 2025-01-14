using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Models
{
    public class UserKey
    {
        public string Username { get; set; }
        public string PrivateKey { get; set; }
        public string PublicKey { get; set; }
        /// <summary>
        /// contains all symetric keys of each chat
        /// </summary>
        public Dictionary<string, string> UsersSymetricKeys { get; set; }
        /// <summary>
        /// contains user's public RSA keys
        /// </summary>
        public Dictionary<string, string> UsersPublicKeys { get; set; }
    }
}
