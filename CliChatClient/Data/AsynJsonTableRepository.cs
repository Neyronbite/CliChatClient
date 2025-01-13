using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace CliChatClient.Data
{
    public class AsynJsonDictionaryFile<TEntity> : IAsyncDisposable
    {
        private readonly string _path;
        //string is unique id, and TEntity is main object
        Dictionary<string, TEntity> _entities;

        public AsynJsonDictionaryFile(string path)
        {
            _path = path;
        }
        public async Task Init()
        {
            if (File.Exists(_path))
            {
                File.Create(_path);
            }

            string text = string.Empty;

            using (var sr = new StreamReader(_path))
            {
                text = await sr.ReadToEndAsync();
                //TODO add encryption
            }

            _entities = JsonConvert.DeserializeObject<Dictionary<string, TEntity>>(text);
        }
        public async virtual Task<List<TEntity>> Get(Func<TEntity, bool> filter)
        {
            IEnumerable<TEntity> query = _entities.Values;
            if (filter != null)
            {
                query = query.Where(filter);
            }
            return query.ToList();
        }
        public async Task<TEntity> GetFirst(Func<TEntity, bool> filter = null)
        {
            IEnumerable<TEntity> query = _entities.Values;
            if (filter != null)
            {
                query = query.Where(filter);
            }
            return query.First();
        }
        public async virtual void Insert(TEntity entity, string id)
        {
            _entities.Add(id, entity);

            await WriteToFileAsync();
        }
        public async virtual void Delete(string id)
        {
            _entities.Remove(id);

            await WriteToFileAsync();
        }
        public async virtual void Update(string id, TEntity entity)
        {
            if (_entities.ContainsKey(id)) 
            {
                _entities.Add(id, entity);
            }
            else
            {
                _entities[id] = entity;
            }

            await WriteToFileAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await WriteToFileAsync();
        }

        private async Task WriteToFileAsync()
        {
            var text = JsonConvert.SerializeObject(_entities);

            using (var sw = new StreamWriter(_path))
            {
                await sw.WriteAsync(text);
            }
        }
    }
}
