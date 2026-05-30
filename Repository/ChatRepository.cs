using System.Collections.Generic;
using System.Threading.Tasks;
using Entities;
using Microsoft.EntityFrameworkCore;

namespace Repository
{
    public class ChatRepository : IChatRepository
    {
        private readonly StoreContext _context;

        public ChatRepository(StoreContext context) => _context = context;

        public async Task<IEnumerable<Book>> GetAllBooksAsync() =>
            await _context.Books.ToListAsync();
    }
}