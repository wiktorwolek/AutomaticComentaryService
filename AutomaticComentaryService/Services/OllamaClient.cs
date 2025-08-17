using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using AutomaticComentaryService.Services;

public sealed class OllamaClient : IOllamaClient
{
    private const int MaxAssistantMemory = 5;
    private readonly HttpClient _http;

    // In-memory per-session state
    private sealed class Session
    {
        public List<OllamaChatMessage> Messages { get; } = new();
        public int[]? Context { get; set; }
        public string Model { get; set; } = "llama3";

        public string LastSystemKey { get; set; } = "";
    }

    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public OllamaClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("ollama");
        // Point to your Ollama instance (override in Program.cs if you prefer)
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri("http://host.docker.internal:11434");
    }
    private const int K = 4;
    public async Task<string> ChatAsync(
        string sessionId,
        string userMessage,
        string? whitelistSystem = null,
        string model = "llama3",
        
        CancellationToken ct = default)
    {
        var session = _sessions.GetOrAdd(sessionId, _ =>
        {
            var s = new Session { Model = model };
            s.Messages.Add(new OllamaChatMessage { Role = "system", Content = Modelfile.SystemPrompt });
            return s;
        });

        string system = Modelfile.SystemPrompt + "\n" + whitelistSystem;
        // Set/refresh model if provided
        string systemKey = $"{model}\n{system}";
        if (!string.Equals(session.LastSystemKey, systemKey, StringComparison.Ordinal))
        {
            session.Context = null;
            session.LastSystemKey = systemKey;
            session.Model = model;
        }

        var reduced = new List<OllamaChatMessage>
    {
        new() { Role = "system", Content = system }
    };

        // keep last K *turns* (user+assistant), not just assistants
        var tail = session.Messages.Where(m => m.Role is "user" or "assistant").TakeLast(K * 2).ToList();
        reduced.AddRange(tail);

        // current turn last
        reduced.Add(new OllamaChatMessage { Role = "user", Content = userMessage });

        var request = new OllamaChatRequest
        {
            Model = model,
            Messages = reduced,
            Stream = false,
            KeepAlive = "10m",
            Context = session.Context,
            Options = new Dictionary<string, object>
            {
                ["temperature"] = 0.25,
                ["top_p"] = 0.9,
                ["repeat_penalty"] = 1.15,
                ["num_ctx"] = 8192,
            }
        };

       

        var requestjson = Newtonsoft.Json.JsonConvert.SerializeObject(request,new Newtonsoft.Json.JsonSerializerSettings { NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore });

        using var content = new StringContent(requestjson, System.Text.Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("api/chat", content, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: ct);
        var reply = json?.Message?.Content?.Trim() ?? string.Empty;

        // Add to full session history (optional, for debugging or logging)
        session.Messages.Add(new OllamaChatMessage { Role = "assistant", Content = reply });

        if (json?.Context is { Length: > 0 })
            session.Context = json.Context;

        return reply;
    }

    public Task ResetSessionAsync(string sessionId)
    {
        _sessions[sessionId] = new Session
        {
            Model = "llama3",
            Messages = { new OllamaChatMessage { Role = "system", Content = Modelfile.SystemPrompt } }
        };
        return Task.CompletedTask;
    }

   

}
