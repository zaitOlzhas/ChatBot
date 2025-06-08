using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChatBotAPI.Services.LLM.Models;

namespace ChatBotAPI.Services.LLM;

public class OllamaServices
{
    public static readonly string OllamaClentName = "OllamaClient";
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaServices> _logger;

    public OllamaServices(IHttpClientFactory httpClientFactory, ILogger<OllamaServices> logger)
    {
        _httpClient = httpClientFactory.CreateClient(OllamaClentName);
        _logger = logger;
    }

    // Pull a model, e.g. "mistral"
    public async Task<bool> PullModelAsync(string modelName, CancellationToken ct)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/pull", new { model = modelName }, ct);
        return response.IsSuccessStatusCode;
    }

    // List all downloaded models
    public async Task<string[]> ListModelsAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetFromJsonAsync<ModelList>("/api/tags", cancellationToken);
        _logger.LogInformation($"List of models: {response}");
        List<string> modelNames = response?.Models?.Select(m => m.Name).ToList() ?? new List<string>();

        return modelNames.ToArray();
    }

    // Delete a model
    public async Task<bool> DeleteModelAsync(string modelName)
    {
        var response = await _httpClient.DeleteAsync($"/api/models/{modelName}");
        return response.IsSuccessStatusCode;
    }

    public async Task<OllamaResponse> UserPromptAsync(string modelName, string prompt, CancellationToken ct)
    {
        var models = await ListModelsAsync(ct);
        if (!models.Any(x=>x.Contains(modelName)))
        {
            _logger.LogWarning($"Model '{modelName}' is not available. Available models: {string.Join(", ", models)}");
            throw new ArgumentException($"Model '{modelName}' is not available. Please pull the model first.");
        }
        return await SendPromptAsync(modelName, prompt, ct);
    }

    private async Task<OllamaResponse> SendPromptAsync(string modelName, string prompt, CancellationToken ct)
    {
        var requestBody = new
        {
            model = modelName,
            stream = false,
            messages = new []
            {
                new { role = "system", content = "Ты помощник в Telegram-чате. Отвечай кратко, понятно и по существу. Избегай длинных вступлений и воды. Использую уместный MarkDown для Telegram." },
                new { role = "user", content = prompt }
            }
        };
        var json = JsonSerializer.Serialize(requestBody);
        _logger.LogInformation($"Sending prompt to Ollama: {json}");
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"/api/chat", content, ct);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OllamaResponse>();
    }
}
public class ModelList
{
    public List<ModelInfo> Models { get; set; } = new();
}

public class ModelInfo
{
    public string Name { get; set; }
    public string Model { get; set; }
}
