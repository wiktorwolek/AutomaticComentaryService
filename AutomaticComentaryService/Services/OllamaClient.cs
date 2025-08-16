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
        public bool HasSystem => Messages.Count > 0 && Messages[0].Role == "system";
    }

    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public OllamaClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("ollama");
        // Point to your Ollama instance (override in Program.cs if you prefer)
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri("http://host.docker.internal:11434");
    }

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


        // Set/refresh model if provided
        var reducedHistory = new List<OllamaChatMessage>();

        // 1. Always keep the system prompt
        reducedHistory.Add(new OllamaChatMessage { Role = "system", Content = Modelfile.SystemPrompt +"\n"+ whitelistSystem});

        // 2. Keep last N assistant messages
        var lastAssistants = session.Messages
            .Where(m => m.Role == "assistant")
            .TakeLast(MaxAssistantMemory)
            .ToList();
        reducedHistory.AddRange(lastAssistants);

        // 3. Add current user prompt
        reducedHistory.Add(new OllamaChatMessage { Role = "user", Content = userMessage });

        var request = new OllamaChatRequest
        {
            Model = model,
            Messages = reducedHistory,
            Stream = false,
            KeepAlive = "10m",
            Context = session.Context,
            Options = new Dictionary<string, object>
            {
                ["temperature"] = 0.2,
                ["top_p"] = 0.9,
                ["repeat_penalty"] = 1.15,
                ["num_ctx"] = 8192,           
                ["stop"] = new[] { "###", "\n\n\n", "welcome", "folks", "stay tuned" }
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

    public void TrimSession(string sessionId, int maxUserAssistantPairs = 10)
    {
        if (!_sessions.TryGetValue(sessionId, out var session)) return;

        var start = session.HasSystem ? 1 : 0;
        var tail = session.Messages.Skip(start).ToList();

        var maxMsgs = Math.Max(0, Math.Min(tail.Count, maxUserAssistantPairs * 2));
        var trimmedTail = tail.Skip(Math.Max(0, tail.Count - maxMsgs)).ToList();

        session.Messages.Clear();
        session.Messages.Add(new OllamaChatMessage { Role = "system", Content = Modelfile.SystemPrompt });
        session.Messages.AddRange(trimmedTail);
    }

}
