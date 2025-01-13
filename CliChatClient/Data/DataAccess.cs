using CliChatClient.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Data
{
    public class DataAccess : IAsyncDisposable
    {
        // Database for storing user's data, including private, public keys, and other exchanged keys
        public AsynJsonDictionaryFile<UserKey> UserKey { get; set; }

        private readonly string basePath;

        public DataAccess()
        {
            basePath = Directory.GetCurrentDirectory();
            UserKey = new AsynJsonDictionaryFile<UserKey>(basePath + "/userKey.json");
        }

        public async Task Init()
        {
            await UserKey.Init();
        }

        public async ValueTask DisposeAsync()
        {
            await UserKey.DisposeAsync();
        }
    }
    //TODO es kareli a interfacerov sirun scalable ban shinel, bayc de vren
}
