using Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository
{
    public class SearchBookRepository : ISearchBookRepository
    {
        private readonly StoreContext _context;

        public SearchBookRepository(StoreContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Book>> GetSuggestionsAsync(string term, int count)
        {
            return await _context.Books
                .Include(b => b.Author)
                .Where(b => b.Title.Contains(term) || b.Author.Name.Contains(term))
                .OrderBy(b => b.Title.StartsWith(term) ? 0 : 1) 
                .Take(count)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<(IEnumerable<Book> Items, int TotalCount)> SearchBooksAsync(string term, int page, int pageSize)
        {
            var query = _context.Books
                .Include(b => b.Author)
                .Include(b => b.Category)
                .Where(b => b.Title.Contains(term) ||
                            b.Author.Name.Contains(term) ||
                            (b.Summary != null && b.Summary.Contains(term)))
                .AsNoTracking();

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(b => b.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
