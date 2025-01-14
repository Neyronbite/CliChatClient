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

        Context _context;

        public DataAccess(Context context)
        {
            _context = context;
            UserKey = new AsynJsonDictionaryFile<UserKey>(context.BasePath + "/userKey.json");
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
