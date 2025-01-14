using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Models
{
    public class Context
    {
        /// <summary>
        /// Username of logged user
        /// </summary>
        public string LoggedUsername { get; set; }
        /// <summary>
        /// Servers base url (https://localhost:7183)
        /// </summary>
        public string BaseUrl { get; set; }
        /// <summary>
        /// Database files base path
        /// Bin folder by default
        /// </summary>
        public string BasePath { get; set; }
        /// <summary>
        /// Authentication jwt token
        /// </summary>
        public string Token { get; set; }
        /// <summary>
        /// if is true, services will ignore certificate validation issues
        /// </summary>
        public bool IgnoreSSL { get; set; }


        public Context()
        {
            //Getting bin folder
            BasePath = Directory.GetCurrentDirectory();
        }
    }
}
