using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities;
using Microsoft.EntityFrameworkCore;

namespace Repository
{
    public class AuthorRepository : IAuthorRepository
    {
        private readonly StoreContext _context;
        public AuthorRepository(StoreContext context) => _context = context;

        public async Task<IEnumerable<Author>> GetAllAsync() =>
            await _context.Authors.Take(100).ToListAsync();

        public async Task<Author> CreateAsync(Author author)
        {
            await _context.Authors.AddAsync(author);
            await _context.SaveChangesAsync();
            return author;
        }

        public async Task<bool> ExistsAsync(int id) =>
            await _context.Authors.AnyAsync(a => a.Id == id);
    }
}
