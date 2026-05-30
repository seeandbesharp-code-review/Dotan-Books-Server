using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Reflection;
using Service;
using DTOs;

namespace DotanBooks.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SearchBooksController : ControllerBase
    {
        private readonly ISearchBookService _searchBookService;
        private readonly IChatService _chatService;
        private readonly HttpClient _http;

        public SearchBooksController(ISearchBookService searchBookService, IChatService chatService, IHttpClientFactory factory)
        {
            _searchBookService = searchBookService;
            _chatService = chatService;
            _http = factory.CreateClient();
        }

        [HttpGet("autocomplete")]
        public async Task<IActionResult> Autocomplete([FromQuery] string term)
        {
            var results = await _searchBookService.GetAutocompleteAsync(term);
            return Ok(results);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string term, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                // 1. ניסיון חיפוש רגיל
                var regularResults = await _searchBookService.GetFullSearchAsync(term, page, pageSize);

                if (regularResults != null && regularResults.TotalCount > 2)
                {
                    return Ok(regularResults);
                }

                // 2. אם לא נמצא מספיק בחיפוש רגיל, פנייה ל-AI
                var allBooks = await _chatService.GetBooksForChatAsync();

                var pythonRequest = new
                {
                    query = term,
                    products = allBooks.Select(b => new
                    {
                        id = b.Id,
                        title = b.Title,
                        summary = b.Summary,
                        imageUrl = b.ImageUrl ?? string.Empty,
                        price = b.Price,
                        authorName = b.AuthorName ?? string.Empty
                    }).ToList()
                };

                var response = await _http.PostAsJsonAsync("http://localhost:8001/search", pythonRequest);

                if (!response.IsSuccessStatusCode)
                {
                    return Ok(regularResults ?? new PagedResponse<BookListDto> { TotalCount = 0, Items = new List<BookListDto>() });
                }

                var jsonText = await response.Content.ReadAsStringAsync();
                var semanticBooks = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(jsonText);

                var totalCount = semanticBooks?.Count ?? 0;

                // הגנה על ערכי ה-Pagination
                int currentPage = page < 1 ? 1 : page;
                int itemsToSkip = (currentPage - 1) * pageSize;

                var pagedItems = semanticBooks?
                    .Skip(itemsToSkip)
                    .Take(pageSize)
                    .Select(b => new BookListDto
                    {
                        Id = b.GetProperty("id").GetInt32(),
                        Title = b.GetProperty("title").GetString(),
                        ImageUrl = b.TryGetProperty("imageUrl", out var imgElement) ? imgElement.GetString() : string.Empty,
                        AuthorName = b.TryGetProperty("authorName", out var authorElement) ? authorElement.GetString() : "לא זמין",
                        Price = b.TryGetProperty("price", out var priceElement) ? priceElement.GetDecimal() : 0m
                    })
                    .ToList() ?? new List<BookListDto>();

                return Ok(new PagedResponse<BookListDto> { TotalCount = totalCount, Items = pagedItems });
            }
            catch (Exception)
            {
                return Ok(new PagedResponse<BookListDto> { TotalCount = 0, Items = new List<BookListDto>() });
            }
        }

        [HttpGet("semantic-search")]
        public async Task<IActionResult> SemanticSearch([FromQuery] string term)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(term)) return BadRequest();

                var allBooks = await _chatService.GetBooksForChatAsync();
                var pythonRequest = new { query = term, products = allBooks };

                var response = await _http.PostAsJsonAsync("http://localhost:8001/search", pythonRequest);
                if (!response.IsSuccessStatusCode) return StatusCode(500);

                var result = await response.Content.ReadFromJsonAsync<dynamic>();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500);
            }
        }
    }
}