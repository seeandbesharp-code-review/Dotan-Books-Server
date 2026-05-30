using DTOs;
using Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Service
{
  public  class SearchBookService:ISearchBookService
    {
        private readonly ISearchBookRepository _repository;

        public SearchBookService(ISearchBookRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<BookAutocompleteDto>> GetAutocompleteAsync(string term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2) return new List<BookAutocompleteDto>();

            var books = await _repository.GetSuggestionsAsync(term, 10);

            return books.Select(static b => new BookAutocompleteDto
            {
                Id = b.Id,
                Title = b.Title,
                Summary = b.Summary ?? string.Empty,
                ImageUrl = b.ImageUrl
            });
        }

        public async Task<PagedResponse<BookListDto>> GetFullSearchAsync(string term, int page, int pageSize)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 10 : pageSize;

            var (items, totalCount) = await _repository.SearchBooksAsync(term, page, pageSize);

            return new PagedResponse<BookListDto>
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Items = items.Select(b => new BookListDto
                {
                    Id = b.Id,
                    Title = b.Title,
                    AuthorName = b.Author.Name,
                    Price = b.Price,
                    ImageUrl = b.ImageUrl
                })
            };
        }

    }
}
