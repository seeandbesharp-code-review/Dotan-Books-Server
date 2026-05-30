using System.Collections.Generic;
using System.Threading.Tasks;
using DTOs;

namespace Service
{
    public interface IChatService
    {
        Task<IEnumerable<BookDto>> GetBooksForChatAsync();
    }
}