using System.Collections.Concurrent;
using AutomaticComentaryService.Constants;
using AutomaticComentaryService.Models;
using AutomaticComentaryService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AutomaticComentaryService.Controllers
{
    [Route("[controller]")]
    public class ComentaryController : ControllerBase
    {
        private readonly IOllamaClient _ollama;
        private readonly ITTSEngine _tts;
        private readonly ILogger<ComentaryController> _logger;

        private static readonly object _stateLock = new();
        private static GameState _lastState = new();
        private static Guid _matchId = Guid.Empty;
        private static Guid _lastMatchId = Guid.Empty;

        public static GameState LastState
        {
            get { lock (_stateLock) return _lastState; }
            set { lock (_stateLock) _lastState = value; }
        }

        public ComentaryController(IOllamaClient ollama, ITTSEngine tts, ILogger<ComentaryController> logger)
        {
            _ollama = ollama;
            _tts = tts;
            _logger = logger;
        }

        // ---------------------------
        // Routes
        // ---------------------------

        [HttpPost("AddEvent")]
        public async Task<IActionResult> AddEvent(CancellationToken cancellationToken = default)
        {
            var rawJson = await ReadRawBodyAsync(cancellationToken);
            var request = JsonConvert.DeserializeObject<ComentaryRequest>(rawJson);
            if (request is null)
                return BadRequest("Invalid payload.");

            ComentaryQueueModel.Instance.MessageQueueEnqueue(request);
            return Ok();
        }

        [HttpPost("AddNewGame")]
        public IActionResult AddNewGame(CancellationToken _ = default)
        {
            _matchId = Guid.NewGuid();
            ComentaryQueueModel.Instance.Reset();
            return Ok(_matchId);
        }

        [HttpGet("GetLast")]
        public async Task<IActionResult> Get(CancellationToken ct = default)
        {
            var last = ComentaryQueueModel.Instance.MessageQueuePeekLast();
            if (last is null || last.GameState is null)
                return BadRequest("No game state available.");

            var currentState = last.GameState;
            var (prompt, whitelistSystem) = BuildPrompt(currentState);

            _logger.LogDebug(prompt);

            // keep your internal call pattern
            return await CommentateAsync(
                prompt: prompt,
                whitelistSystem: whitelistSystem,
                gamestate: currentState,
                matchid: _matchId.ToString(),
                ct: ct);
        }

        // NOTE: This route signature is fine because you call it internally.
        // If you ever want to POST from outside, create a DTO request body instead.
        [HttpPost("comentate")]
        public async Task<IActionResult> CommentateAsync(
            [FromBody] string prompt,
            string? whitelistSystem,
            GameState gamestate,
            string matchid = "random",
            CancellationToken ct = default)
        {
            var sessionId = matchid;

            // 1) generate
            var commentary = await GenerateCommentaryAsync(sessionId, prompt, whitelistSystem, ct);

            // 2) validate/repair
            commentary = await ValidateOrRepairAsync(sessionId, commentary, whitelistSystem, gamestate, ct);

            // 3) persist + tts
            _logger.LogDebug(commentary);
            await SavePromptAndCommentaryAsync(sessionId, prompt, commentary, ct);

            var filename = await _tts.GenerateWavAsync(commentary);
            return Ok(new { commentary, audioFile = filename });
        }

        // ---------------------------
        // Helpers: Prompt building
        // ---------------------------

        private (string Prompt, string? Whitelist) BuildPrompt(GameState currentState)
        {
            var isNewMatch = _lastMatchId != _matchId;

            var prompt = CommentaryPromptBuilder.BuildPrompt(
                currentGameState: currentState,
                lastGameState: LastState,
                isFirstUpdate: isNewMatch);

            LastState = currentState;
            _lastMatchId = _matchId;

            var whitelist = CommentaryWhitelistPrompt.Build(currentState);
            return (prompt, whitelist);
        }

        // ---------------------------
        // Helpers: LLM I/O
        // ---------------------------

        private async Task<string> GenerateCommentaryAsync(
            string sessionId,
            string prompt,
            string? whitelistSystem,
            CancellationToken ct)
        {
            var commentary = await _ollama.ChatAsync(
                sessionId: sessionId,
                userMessage: prompt,
                whitelistSystem: whitelistSystem,
                model: "llama3.1:8b",
                ct: ct);

            return commentary ?? string.Empty;
        }

        private async Task<string> ValidateOrRepairAsync(
            string sessionId,
            string commentary,
            string? whitelistSystem,
            GameState gamestate,
            CancellationToken ct)
        {
            var (validTeams, validPlayers, validRoles) = BuildNameSetsFromGameState(gamestate);

            // First pass: validate
            var (ok, _) = CommentaryValidator.Validate(
                commentary, validTeams, validPlayers, validRoles, BannedPhrases.Set);

            if (ok) return commentary;


            // Build a repair prompt and ask the model to rewrite
            var repaired = await RepairWithModelAsync(
                sessionId, commentary, whitelistSystem, validTeams, validPlayers, validRoles, ct);

            var (okRepaired, _) = CommentaryValidator.Validate(
                repaired, validTeams, validPlayers, validRoles, BannedPhrases.Set);

            // Prefer repaired if valid, otherwise fallback to whatever we salvaged (possibly empty)
            return repaired;
        }

        private async Task<string> RepairWithModelAsync(
            string sessionId,
            string badOutput,
            string? whitelistSystem,
            ISet<string> validTeams,
            ISet<string> validPlayers,
            ISet<string> validRoles,
            CancellationToken ct)
        {
            var repairPrompt = CommentaryValidator.BuildRepairPrompt(
                badOutput: badOutput,
                examples: BuildStyleExamples(),
                validTeams: validTeams,
                validPlayers: validPlayers,
                validRoles: validRoles);

            var repaired = await _ollama.ChatAsync(
                sessionId: sessionId,
                userMessage: repairPrompt,
                whitelistSystem: whitelistSystem,
                model: "llama3.1:8b",
                ct: ct);

            return repaired ?? string.Empty;
        }

        private static IEnumerable<string> BuildStyleExamples() => new[]
        {
            "marcus windcaller tucks into a fresh cage — ironclad warriors lock it down.",
            "owen thunderstrike drifts off a corner — gaps open in the cage.",
            "blackwood and redcliff close in — pressure surges on the carrier."
        };

        // ---------------------------
        // Helpers: Persistence
        // ---------------------------

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

        // ---------------------------
        // Helpers: Names & Body
        // ---------------------------

        private static (ISet<string> Teams, ISet<string> Players, ISet<string> Roles) BuildNameSetsFromGameState(GameState? state)
        {
            var teams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var players = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (state?.Teams != null)
            {
                foreach (var t in state.Teams)
                {
                    if (!string.IsNullOrWhiteSpace(t?.Name)) teams.Add(t.Name);
                    if (t?.Players == null) continue;

                    foreach (var p in t.Players)
                    {
                        if (!string.IsNullOrWhiteSpace(p?.Name)) players.Add(p.Name);
                        if (!string.IsNullOrWhiteSpace(p?.Role)) roles.Add(p.Role);
                    }
                }
            }

            return (teams, players, roles);
        }

        private async Task<string> ReadRawBodyAsync(CancellationToken ct)
        {
            using var reader = new StreamReader(Request.Body);
            return await reader.ReadToEndAsync(ct);
        }
    }
}
