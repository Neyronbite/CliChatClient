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
        public HttpStatusCode StatusCode { get; set; }
        public string Message { get; set; }
        public string? Suggestions { get; set; }
    }
}
