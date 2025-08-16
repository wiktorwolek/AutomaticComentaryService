using System.ComponentModel;
using System.Diagnostics;
using AutomaticComentaryService.Models;
using AutomaticComentaryService.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace AutomaticComentaryService.Controllers
{
    
    [Route("[controller]")]
    public class ComentaryController : ControllerBase
    {
        private  readonly IOllamaClient _ollama;
        private  readonly ITTSEngine _tts;
        //private  readonly RagAssistant ragger = new RagAssistant(new string[] { "/app/RagDatabase" });
        private readonly ILogger<ComentaryController> _logger;
        private static readonly object _stateLock = new();
        private static GameState _lastState = new();
        private static Guid matchid = new();
        private static Guid lastMatchid = new();
        public static GameState LastState
        {
            get { lock (_stateLock) return _lastState; }
            set { lock (_stateLock) _lastState = value; }
        }

        public ComentaryController(IOllamaClient ollama, ITTSEngine tts, ILogger<ComentaryController> logger)
        {
            var stopwatch = Stopwatch.StartNew();
            _ollama = ollama;
            _tts = tts;
            _logger = logger;
        }

        
        [HttpPost("AddEvent")]
        public  async Task<IActionResult> AddEvent( CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(Request.Body);
            var rawJson = await reader.ReadToEndAsync(cancellationToken);
            var request = JsonConvert.DeserializeObject<ComentaryRequest>(rawJson);
            ComentaryQueueModel.Instance.MessageQueueEnqueue(request);

            return Ok();
        }
        

        [HttpPost("AddNewGame")]
        public IActionResult AddNewGame(CancellationToken cancellationToken = default)
        {
            matchid = Guid.NewGuid();

            ComentaryQueueModel.Instance.Reset();
            return Ok(matchid);
        }

        [HttpGet("GetLast")]
        public async Task<IActionResult> Get()
        {
            string prompt = "";
            string? whitelistSystem = null;
            if (ComentaryQueueModel.Instance.GetMessageCount() == 0)
            {
                prompt = CommentaryPromptBuilder.BuildStalingPrompt();
            }
            else
            {
                var currentState = ComentaryQueueModel.Instance.MessageQueuePeek().GameState;
                prompt = CommentaryPromptBuilder.BuildPrompt( currentState, ComentaryController.LastState, lastMatchid!=matchid) ;
                ComentaryController.LastState = currentState;
                lastMatchid = matchid;
                whitelistSystem = CommentaryWhitelistPrompt.Build(currentState);
            }
            _logger.LogDebug(prompt);
            
            return await CommentateAsync(prompt,whitelistSystem, matchid.ToString());
        }
        [HttpPost("comentate")]
        public async Task<IActionResult> CommentateAsync(
            [FromBody] string prompt,string? whitelistSystem,string matchid = "random",
                CancellationToken ct = default)
        {
            var sessionId = matchid ;

            var commentary = await _ollama.ChatAsync(
                sessionId: sessionId,
                userMessage: prompt,
                whitelistSystem: whitelistSystem,
                model: "llama3:8b",
                ct);
            _logger.LogDebug(commentary);
            await SavePromptAndCommentaryAsync(sessionId, prompt, commentary, ct);
            var filename = await _tts.GenerateWavAsync(commentary);
            return Ok(new { commentary, audioFile = filename });
        }

        private async Task SavePromptAndCommentaryAsync(
    string sessionId,
    string prompt,
    string commentary,
    CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow;
            var timestamp = now.ToString("yyyyMMdd_HHmmssfff");
            var safeSession = SanitizeForFileName(sessionId);

            var baseDir = Environment.GetEnvironmentVariable("DATA_DIR")
                         ?? Path.Combine(AppContext.BaseDirectory, "data");

            var dir = Path.Combine(baseDir, now.ToString("yyyy"), now.ToString("MM"), now.ToString("dd"), safeSession);
            Directory.CreateDirectory(dir);

            var promptPath = Path.Combine(dir, $"{timestamp}_{safeSession}_prompt.txt");
            var commentaryPath = Path.Combine(dir, $"{timestamp}_{safeSession}_commentary.txt");

            await System.IO.File.WriteAllTextAsync(promptPath, prompt ?? string.Empty, ct);
            await System.IO.File.WriteAllTextAsync(commentaryPath, commentary ?? string.Empty, ct);
        }

        private static string SanitizeForFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "unknown";
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(input.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
            return sanitized.Length == 0 ? "unknown" : sanitized;
        }



    }
}
