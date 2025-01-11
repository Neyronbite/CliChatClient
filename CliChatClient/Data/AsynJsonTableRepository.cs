using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Data
{
    public class AsynJsonTableRepository<TEntity>
    {
        private readonly string _path;
        List<TEntity> _entities;

        public AsynJsonTableRepository(string path)
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

            _entities = JsonConvert.DeserializeObject<List<TEntity>>(text);
        }
        public async virtual Task<List<TEntity>> Get(Func<TEntity, bool> filter)
        {
            IEnumerable<TEntity> query = _entities;
            if (filter != null)
            {
                query = query.Where(filter);
            }
            return query.ToList();
        }
        public async Task<TEntity> GetFirst(Func<TEntity, bool> filter = null)
        {
            IEnumerable<TEntity> query = _entities;
            if (filter != null)
            {
                query = query.Where(filter);
            }
            return query.First();
        }
        public async virtual void Insert(TEntity entity)
        {
            _entities.Add(entity);
            var text = JsonConvert.SerializeObject(_entities);

            using (var sw = new StreamWriter(_path))
            {
                await sw.WriteAsync(text);
            }
        }
        public async virtual void Delete(TEntity entityToDelete)
        {
            _entities.Remove(entityToDelete);

            var text = JsonConvert.SerializeObject(_entities);

            using (var sw = new StreamWriter(_path))
            {
                await sw.WriteAsync(text);
            }
        }
    }
}
