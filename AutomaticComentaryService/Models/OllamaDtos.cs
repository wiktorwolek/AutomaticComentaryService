using System.Text.Json.Serialization;
using Newtonsoft.Json;

public sealed class OllamaChatMessage
{
    [JsonProperty("role")] public string Role { get; set; } = default!;   // "system" | "user" | "assistant"
    [JsonProperty("content")] public string Content { get; set; } = default!;
}

public sealed class OllamaChatRequest
{
    [JsonProperty("model")] public string Model { get; set; } = "llama3";
    [JsonProperty("messages")] public List<OllamaChatMessage> Messages { get; set; } = new();
    [JsonProperty("stream")] public bool Stream { get; set; } = false;

    // Optional knobs:
    [JsonProperty("keep_alive")] public string? KeepAlive { get; set; } = "10m";
    [JsonProperty("context")] public int[]? Context { get; set; }
}

public sealed class OllamaChatResponse
{
    [JsonProperty("model")] public string? Model { get; set; }
    [JsonProperty("message")] public OllamaChatMessage? Message { get; set; }
    [JsonProperty("done")] public bool Done { get; set; }
    [JsonProperty("context")] public int[]? Context { get; set; }
    [JsonProperty("total_duration")] public long? TotalDuration { get; set; }
}
