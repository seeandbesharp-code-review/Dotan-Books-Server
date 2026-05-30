using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DTOs;
using Repository;

namespace Service
{
    public class ChatService : IChatService
    {
        private readonly IChatRepository _chatRepository;

        public ChatService(IChatRepository chatRepository) => _chatRepository = chatRepository;

        public async Task<IEnumerable<BookDto>> GetBooksForChatAsync()
        {
            var books = await _chatRepository.GetAllBooksAsync();
            return books.Select(b => new BookDto
            {
                Id = b.Id,
                Title = b.Title,
                Summary = b.Summary,
                ImageUrl = b.ImageUrl, 
                Price = b.Price,
                AuthorName = b.Author?.Name,
            }).ToList();
        }
    }
}