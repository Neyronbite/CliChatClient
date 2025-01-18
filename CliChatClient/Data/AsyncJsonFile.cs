using CliChatClient.Models;
using CliChatClient.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace CliChatClient.Data
{
    public class AsyncJsonFile<T> : IJsonFile<T>
    {
        protected string _path;

        protected Context _context;

        //string is unique id, and T is main object
        private Dictionary<string, T> _cache;

        public AsyncJsonFile(Context context)
        {
            _context = context;
        }
        
        /// <summary>
        /// getting T from cashed data
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public virtual T this[string id]
        {
            get
            {
                if (_cache.ContainsKey(id))
                {
                    return _cache[id];
                }

                throw new ArgumentException($"Item '{id}' not found");
            }
        }

        public async virtual Task Init()
        {
            _path = $"{_context.BasePath}/{_context.BaseUrl.Encode()}.json";

            if (!File.Exists(_path))
            {
                using (File.Create(_path)) { }
            }

            _cache = await ReadFromFile<T>();
        }

        public async virtual Task Delete(string id)
        {
            _cache = await ReadFromFile<T>();

            _cache.Remove(id);

            await WriteToFileAsync(_cache);
        }

        public async virtual Task Update(string id, T item)
        {
            _cache = await ReadFromFile<T>();

            if (_cache.ContainsKey(id))
            {
                _cache[id] = item;
            }
            else
            {
                _cache.Add(id, item);
            }

            await WriteToFileAsync(_cache);
        }

        protected virtual async Task WriteToFileAsync(object obj)
        {
            var text = JsonConvert.SerializeObject(obj);

            using (var sw = new StreamWriter(_path))
            {
                await sw.WriteAsync(text);
            }
        }

        protected virtual async Task<Dictionary<string, TObj>> ReadFromFile<TObj>()
        {
            var text = string.Empty;

            using (var sr = new StreamReader(_path))
            {
                text = await sr.ReadToEndAsync();
            }

            var json = JsonConvert.DeserializeObject<Dictionary<string, TObj>>(text);

            if (json == null)
            {
                json = new Dictionary<string, TObj>();
            }

            return json;
        }
    }
}
