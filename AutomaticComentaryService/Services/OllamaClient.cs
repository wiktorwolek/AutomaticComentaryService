using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ollama; // Ensure that you have installed the necessary Ollama NuGet package

namespace AutomaticComentaryService.Services
{
    public class OllamaClient : IOllamaClient
    {
        private readonly OllamaApiClient _ollamaApiClient;

        // The base URL is fixed but can be overridden if needed.
        public OllamaClient(HttpClient httpClient, string baseUrl = "http://host.docker.internal:11434")
        {
            
            // Create the API client with the /api endpoint appended.
            _ollamaApiClient = new OllamaApiClient(httpClient, new Uri(baseUrl + "/api"));
        }

        /// <inheritdoc />
        public async Task<string> GenerateCompletionAsync(string prompt, string model = "llama2", CancellationToken cancellationToken = default)
        {
            var response = await _ollamaApiClient.Completions.GenerateCompletionAsync(
                model,
                prompt,
                stream: false,
                cancellationToken: cancellationToken);

            // Return the completion text (or empty if null)
            return response?.Response ?? string.Empty;
        }
    }
}
