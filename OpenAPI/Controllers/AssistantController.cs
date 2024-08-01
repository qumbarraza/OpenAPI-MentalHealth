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
    public class AssistantController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly OpenAIOptions _options;

        public AssistantController(IHttpClientFactory httpClientFactory, IOptions<OpenAIOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        [HttpPost("create-assistant")]
        public async Task<IActionResult> CreateAssistant([FromBody] CreateAssistantRequest request)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            client.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");

            var assistantRequest = new
            {
                instructions = request.Instructions,
                name = request.Name,
                tools = request.Tools.Select(tool => new { type = tool }).ToArray(),
                model = request.Model
            };

            var content = new StringContent(JsonConvert.SerializeObject(assistantRequest), Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/assistants", content);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return Ok(result);
            }
            else
            {
                var errorResponse = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, errorResponse);
            }
        }

        [HttpPost("create-thread")]
        public async Task<IActionResult> CreateThread()
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            client.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");

            var content = new StringContent("", Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/threads", content);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return Ok(result);
            }
            else
            {
                var errorResponse = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, errorResponse);
            }
        }

        [HttpPost("add-message")]
        public async Task<IActionResult> AddMessage([FromBody] MessageRequest request)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            client.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");

            var messageRequest = new
            {
                role = "user",
                content = request.Prompt
            };

            var content = new StringContent(JsonConvert.SerializeObject(messageRequest), Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"https://api.openai.com/v1/threads/{request.ThreadId}/messages", content);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return Ok(result);
            }
            else
            {
                var errorResponse = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, errorResponse);
            }
        }

        [HttpPost("create-run")]
        public async Task<IActionResult> CreateRun([FromBody] RunRequest request)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            client.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");

            // Add user prompt as a message to the thread
            var userMessageRequest = new
            {
                role = "user",
                content = request.UserPrompt
            };

            var userMessageContent = new StringContent(JsonConvert.SerializeObject(userMessageRequest), Encoding.UTF8, "application/json");
            var userMessageResponse = await client.PostAsync($"https://api.openai.com/v1/threads/{request.ThreadId}/messages", userMessageContent);

            if (!userMessageResponse.IsSuccessStatusCode)
            {
                var errorUserMessageResponse = await userMessageResponse.Content.ReadAsStringAsync();
                return StatusCode((int)userMessageResponse.StatusCode, errorUserMessageResponse);
            }

            // Create run
            var runRequest = new
            {
                assistant_id = request.AssistantId,
                instructions = request.Instructions
            };

            var runContent = new StringContent(JsonConvert.SerializeObject(runRequest), Encoding.UTF8, "application/json");
            var runResponse = await client.PostAsync($"https://api.openai.com/v1/threads/{request.ThreadId}/runs", runContent);

            if (!runResponse.IsSuccessStatusCode)
            {
                var errorRunResponse = await runResponse.Content.ReadAsStringAsync();
                return StatusCode((int)runResponse.StatusCode, errorRunResponse);
            }

            var runResult = await runResponse.Content.ReadFromJsonAsync<RunResponse>();

            // Polling for run completion
            string runStatus = "running";
            while (runStatus == "running")
            {
                await Task.Delay(2000); // Polling interval

                var runStatusResponse = await client.GetAsync($"https://api.openai.com/v1/threads/{request.ThreadId}/runs/{runResult.Id}");
                if (!runStatusResponse.IsSuccessStatusCode)
                {
                    var errorStatusResponse = await runStatusResponse.Content.ReadAsStringAsync();
                    return StatusCode((int)runStatusResponse.StatusCode, errorStatusResponse);
                }

                var runStatusResult = await runStatusResponse.Content.ReadFromJsonAsync<RunStatusResponse>();
                runStatus = runStatusResult.Status;
            }

            // Retrieve messages from the thread
            var messagesResponse = await client.GetAsync($"https://api.openai.com/v1/threads/{request.ThreadId}/messages");
            if (!messagesResponse.IsSuccessStatusCode)
            {
                var errorMessagesResponse = await messagesResponse.Content.ReadAsStringAsync();
                return StatusCode((int)messagesResponse.StatusCode, errorMessagesResponse);
            }

            var messagesResult = await messagesResponse.Content.ReadAsStringAsync();
            return Ok(messagesResult);
        }
    }
    public class CreateAssistantRequest
    {
        public string Instructions { get; set; }
        public string Name { get; set; }
        public string[] Tools { get; set; }
        public string Model { get; set; }
    }

    public class RunResponse
    {
        public string Id { get; set; }
    }

    public class RunStatusResponse
    {
        public string Status { get; set; }
    }
    public class MessageRequest
    {
        public string ThreadId { get; set; }
        public string Prompt { get; set; }
    }

    public class RunRequest
    {
        public string ThreadId { get; set; }
        public string AssistantId { get; set; }
        public string Instructions { get; set; }
        public string UserPrompt { get; set; }

    }


}
