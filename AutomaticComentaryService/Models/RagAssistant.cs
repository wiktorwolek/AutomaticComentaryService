using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using System.Threading.Tasks;

public class RagAssistant
{
    private readonly HttpClient _http = new();
    private readonly List<(string Chunk, float[] Embedding)> _vectorStore = new();
    private const int ChunkSize = 500;
    private const int ChunkOverlap = 50;
    private const string EmbeddingModel = "nomic-embed-text";

    public RagAssistant(string[] directories)
    {
        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                Console.WriteLine($"Directory does not exist: {dir}");
                continue;
            }

            var pdfFiles = Directory.GetFiles(dir, "*.pdf", SearchOption.AllDirectories);

            foreach (var file in pdfFiles)
            {
                try
                {
                    var text = ExtractTextFromPdf(file);
                    var chunks = SplitText(text, ChunkSize, ChunkOverlap);
                    foreach (var chunk in chunks)
                    {
                        var embedding = GetEmbeddingAsync(chunk).Result;
                        _vectorStore.Add((chunk, embedding));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to process {file}: {ex.Message}");
                }
            }
        }
    }

    public async Task<string> RagPrompt(string basePrompt, int topK = 3)
    {
        var questionEmbedding = await GetEmbeddingAsync(basePrompt);

        var ranked = _vectorStore
            .Select(x => new { x.Chunk, Score = CosineSimilarity(questionEmbedding, x.Embedding) })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Chunk)
            .ToList();

        var context = string.Join("\n\n", ranked);
        var prompt = $"Use the following context from rulebook and guides to generate commentary:\n\n{context}\n\n Action: {basePrompt}";

        return prompt;
    }

    private string ExtractTextFromPdf(string filePath)
    {
        var sb = new StringBuilder();
        using var pdf = PdfDocument.Open(filePath);
        foreach (Page page in pdf.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        return sb.ToString();
    }

    private List<string> SplitText(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        for (int i = 0; i < text.Length; i += chunkSize - overlap)
        {
            int len = Math.Min(chunkSize, text.Length - i);
            chunks.Add(text.Substring(i, len));
        }
        return chunks;
    }

    private async Task<float[]> GetEmbeddingAsync(string text)
    {
        var response = await _http.PostAsJsonAsync("http://host.docker.internal:11434/api/embeddings", new
        {
            model = EmbeddingModel,
            prompt = text
        });

        var json = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(json).RootElement;
        var embedding = root.GetProperty("embedding").EnumerateArray().Select(x => x.GetSingle()).ToArray();
        return embedding;
    }

    private float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        return dot / ((float)Math.Sqrt(magA) * (float)Math.Sqrt(magB));
    }
}
