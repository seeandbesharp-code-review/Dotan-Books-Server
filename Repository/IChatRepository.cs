using System.Collections.Generic;
using System.Threading.Tasks;
using Entities;

namespace Repository
{
    public interface IChatRepository
    {
        Task<IEnumerable<Book>> GetAllBooksAsync();
    }
}