using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Data
{
    public interface IJsonFile<T>
    {
        T this[string id] { get; }

        Task Init();
        Task Delete(string id);
        Task Update(string id, T item);
    }
}
