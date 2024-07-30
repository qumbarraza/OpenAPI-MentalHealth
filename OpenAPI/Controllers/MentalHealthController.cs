using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;


namespace OpenAPI.Controllers
{
    
    [ApiController]
    [Route("api/[controller]")]
    public class MentalHealthController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly OpenAIOptions _options;

        public MentalHealthController(IHttpClientFactory httpClientFactory, IOptions<OpenAIOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var messages = new[]
            {
            new
            {
                role = "system",
                content = new[]
                {
                    new { type = "text", text = "You are a Psychiatrist who can identify mental health" }
                }
            },
            new
            {
                role = "user",
                content = new[]
                {
                    new { type = "text", text = request.Prompt }
                }
            }
        };

            var openAiRequest = new
            {
                model = "gpt-4",
                messages,
                temperature = 1,
                max_tokens = 256,
                top_p = 1,
                frequency_penalty = 0,
                presence_penalty = 0
            };

            var content = new StringContent(JsonConvert.SerializeObject(openAiRequest), Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OpenAiResponse>();
                return Ok(result);
            }
            else
            {
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
            }
        }
    }

    public class ChatRequest
    {
        public string Prompt { get; set; }
    }

    public class OpenAiResponse
    {
        public string Id { get; set; }
        public string Object { get; set; }
        public int Created { get; set; }
        public Choice[] Choices { get; set; }
        public Usage Usage { get; set; }
    }

    public class Choice
    {
        public Message Message { get; set; }
        public int Index { get; set; }
        public string FinishReason { get; set; }
    }

    public class Message
    {
        public string Role { get; set; }
        public string Content { get; set; } // Changed to string
    }

    public class Usage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    public class OpenAIOptions
    {
        public string ApiKey { get; set; }
    }
}
