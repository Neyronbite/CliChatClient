using CliChatClient.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Data
{
    public class DataAccess
    {
        public AsynJsonTableRepository<UserKey> UserKey { get; set; }

        private readonly string basePath;

        public DataAccess()
        {
            basePath = Directory.GetCurrentDirectory();
            UserKey = new AsynJsonTableRepository<UserKey>(basePath + "/userKey.json");
        }
    }
    //TODO es kareli a interfacerov sirun scalable ban shinel, bayc de vren
}
