using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using Service;
using DTOs;

namespace DotanBooks.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly HttpClient _http;
        private readonly IChatService _chatService;

        public ChatController(IHttpClientFactory factory, IChatService chatService)
        {
            _http = factory.CreateClient();
            _chatService = chatService;
        }

        [HttpPost]
        public async Task<IActionResult> AskDotan([FromBody] ChatRequestDto request)
        {
            try
            {
                var booksFromDb = await _chatService.GetBooksForChatAsync();

                var pythonRequest = new
                {
                    message = request.Message,
                    history = request.History ?? new List<object>(),
                    products = booksFromDb.Select(b => new {
                        id = b.Id,
                        title = b.Title,
                        summary = b.Summary
                    }).ToList()
                };

                var response = await _http.PostAsJsonAsync("http://localhost:8001/chat", pythonRequest);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "שגיאה בתקשורת עם מנוע ה-AI");
                }

                var result = await response.Content.ReadFromJsonAsync<dynamic>();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"שגיאה פנימית במערכת: {ex.Message}");
            }
        }
    }

    public class ChatRequestDto
    {
        public string Message { get; set; } = string.Empty;
        public List<object>? History { get; set; }
    }
}