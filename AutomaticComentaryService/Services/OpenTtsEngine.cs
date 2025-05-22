using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace AutomaticComentaryService.Services
{
    public class OpenTtsEngine : ITTSEngine
    {
        private readonly HttpClient _http;
        private readonly string _voiceId;

        
        public OpenTtsEngine(string baseUrl = "http://host.docker.internal:5500", string voiceId = "coqui-tts:en_ljspeech")
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
            _voiceId = voiceId;
        }

        public async Task<string> GenerateWavAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text must not be empty", nameof(text));

            // Build the query: /api/tts?voice={voice}&text={text}
            var query = $"/api/tts?voice={Uri.EscapeDataString(_voiceId)}&text={Uri.EscapeDataString(text)}";

            using var response = await _http.GetAsync(query, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            // Read the raw WAV bytes
            var audioBytes = await response.Content.ReadAsByteArrayAsync();

            // Save to a unique .wav file
            var fileName = Path.Combine(
                Path.GetTempPath(),
                $"opentts_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.wav"
            );
            await File.WriteAllBytesAsync(fileName, audioBytes);

            return fileName;
        }
    }
}
