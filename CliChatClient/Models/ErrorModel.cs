using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Models
{
    public class ErrorModel
    {
        public HttpStatusCode Status { get; set; }
        public string Title { get; set; }
        public Dictionary<string, string[]> Errors { get; set; }
    }
}
