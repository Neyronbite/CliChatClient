using CliChatClient.Helpers;
using CliChatClient.Models;
using CliChatClient.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Data
{
    public class AsyncEncryptedJsonFile<T> : AsyncJsonFile<T>
    {
        // TODO add semaphor for async work of 2 or more running apps
        protected string _password;

        // cashed file data. Ssring is unique id, and string is encrypted T object
        Dictionary<string, string> _cache;

        public AsyncEncryptedJsonFile(Context context) : base(context)
        {
        }

        public override T this[string id]
        {
            get
            {
                if (_cache.ContainsKey(id))
                {
                    string encrypted = _cache[id];
                    string decrypted = string.Empty;
                    T item = default;

                    try
                    {
                        decrypted = AESEncryptionHelper.PasswordDecrypt(encrypted, _password);
                    }
                    catch (Exception)
                    {
                        throw new ArgumentException($"Wrong password for item '{id}'");
                    }

                    item = JsonConvert.DeserializeObject<T>(decrypted);
                    return item;
                }

                throw new ArgumentException($"Item '{id}' not found");
            }
        }

        public async override Task Init()
        {
            _password = _context.Password;
            _path = $"{_context.BasePath}/{_context.BaseUrl.Encode()}.json";

            if (!File.Exists(_path))
            {
                using (File.Create(_path)) { }
            }

            _cache = await ReadFromFile<string>();
        }

        public async override Task Delete(string id)
        {
            _cache = await ReadFromFile<string>();

            // var t = this[id];
            _cache.Remove(id);

            await WriteToFileAsync(_cache);
        }

        public async override Task Update(string id, T item)
        {
            var json = JsonConvert.SerializeObject(item);
            var encrypted = AESEncryptionHelper.PasswordEncrypt(json, _password);

            _cache = await ReadFromFile<string>();

            if (_cache.ContainsKey(id))
            {
                // getting password decrypted, to check if password is wrong
                // var t = this[id];

                _cache[id] = encrypted;
            }
            else
            {
                _cache[id] = encrypted;
            }

            await WriteToFileAsync(_cache);
        }
    }
}
