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
        public IJsonFile<UserKey> UserKeys { get; set; }

        Context _context;

        public DataAccess(Context context, bool encrypt = true)
        {
            _context = context;

            //TODO use factory
            if (encrypt)
            {
                UserKeys = new AsyncEncryptedJsonFile<UserKey>(context);
            }
            else
            {
                UserKeys = new AsyncJsonFile<UserKey>(context);
            }
        }

        public async Task Init()
        {
            await UserKeys.Init();
        }

        public async ValueTask DisposeAsync()
        {
            //await UserKeys.DisposeAsync();
        }
    }
}
