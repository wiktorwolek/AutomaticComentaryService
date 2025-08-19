using System.Collections.Concurrent;
using System.Text;
using AutomaticComentaryService.Constants;
using AutomaticComentaryService.Models;
using AutomaticComentaryService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

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

            var dto = new ComentaryDto
            {
                Prompt = prompt,
                WhitelistSystem = whitelistSystem,
                GameState = currentState,
                MatchId = _matchId.ToString()
            };

            // keep your internal call pattern
            return await CommentateAsync(dto, ct);
        }


        [HttpPost("comentate")]
        public async Task<IActionResult> CommentateAsync(
        [FromBody] ComentaryDto dto,
         CancellationToken ct = default)
        {
            await SaveComentateBundleAsync(
    sessionId: dto.MatchId,
    prompt: dto.Prompt,
    whitelistSystem: dto.WhitelistSystem,
    gameState: dto.GameState,
    ct: ct);
            var sessionId = dto.MatchId;

            // 1) generate
            var commentary = await GenerateCommentaryAsync(
                sessionId: sessionId,
                prompt: dto.Prompt,
                whitelistSystem: dto.WhitelistSystem,
                ct: ct);

            // 2) validate/repair
            commentary = await ValidateOrRepairAsync(
                sessionId: sessionId,
                commentary: commentary,
                whitelistSystem: dto.WhitelistSystem,
                gamestate: dto.GameState,
                ct: ct);

            // 3) persist + tts
            _logger.LogDebug(commentary);

            await SavePromptAndCommentaryAsync(sessionId, dto.Prompt, commentary, ct);

            var filename = await _tts.GenerateWavAsync(commentary);
            return Ok(new { commentary, audioFile = filename });
        }



        // ---------------------------
        // Helpers: Prompt building
        // ---------------------------

       

        public class ComentateReplayEnvelope
        {
            public string Schema { get; set; } = "comentate-replay.v1";
            public string TimestampUtc { get; set; } = DateTimeOffset.UtcNow.ToString("O");
            public string SessionId { get; set; } = string.Empty;

            // Exact payload for /comentate
            public ComentaryDto Request { get; set; } = new();

            // SHA-256 of canonicalized Request (stable, property-sorted JSON)
            public string ContentHashSha256 { get; set; } = string.Empty;
        }

        public sealed record LoadedComentateBundle(
            ComentaryDto Request,
            bool IntegrityOk,
            string ExpectedHash,
            string ActualHash,
            string SourcePath);

        private async Task<string> SaveComentateBundleAsync(
            string sessionId,
            string prompt,
            string? whitelistSystem,
            GameState gameState,
            CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow;
            var timestamp = now.ToString("yyyyMMdd_HHmmssfff");
            var safeSession = SanitizeForFileName(sessionId);

            var baseDir = Environment.GetEnvironmentVariable("DATA_DIR")
                          ?? Path.Combine(AppContext.BaseDirectory, "data");
            var dir = Path.Combine(baseDir, now.ToString("yyyy"), now.ToString("MM"), now.ToString("dd"), safeSession);
            Directory.CreateDirectory(dir);

            var requestDto = new ComentaryDto
            {
                Prompt = prompt ?? string.Empty,
                WhitelistSystem = whitelistSystem,
                GameState = gameState ?? new GameState(),
                MatchId = sessionId
            };

            // Canonical JSON (property-sorted, minified) → stable hash
            var canonical = ToCanonicalJson(requestDto);
            var hash = Sha256Hex(canonical);

            var envelope = new ComentateReplayEnvelope
            {
                SessionId = sessionId,
                TimestampUtc = now.ToString("O"),
                Request = requestDto,
                ContentHashSha256 = hash
            };

            var pretty = JsonConvert.SerializeObject(
                envelope,
                new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

            // Include short hash fragment in filename for quick grepping
            var shortHash = hash[..12];
            var bundlePath = Path.Combine(dir, $"{timestamp}_{safeSession}_{shortHash}_comentate.bundle.json");

            await System.IO.File.WriteAllTextAsync(bundlePath, pretty, ct);
            return bundlePath;
        }

        private async Task<LoadedComentateBundle?> LoadComentateBundleAsync(
            string path,
            CancellationToken ct = default)
        {
            if (!System.IO.File.Exists(path)) return null;

            var json = await System.IO.File.ReadAllTextAsync(path, ct);
            var env = JsonConvert.DeserializeObject<ComentateReplayEnvelope>(json);
            if (env is null) return null;

            // Recompute hash to verify integrity / sameness
            var canonical = ToCanonicalJson(env.Request);
            var actual = Sha256Hex(canonical);
            var expected = env.ContentHashSha256 ?? string.Empty;

            return new LoadedComentateBundle(
                Request: env.Request,
                IntegrityOk: string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                ExpectedHash: expected,
                ActualHash: actual,
                SourcePath: path
            );
        }

        // ---------- Canonicalization + Hash helpers ----------

        private static string ToCanonicalJson(object obj)
        {
            // 1) Convert to JToken with safe settings
            var serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });

            var token = JToken.FromObject(obj, serializer);

            // 2) Recursively sort object properties by name (arrays kept in order)
            static JToken Canon(JToken t) =>
                t switch
                {
                    JObject o => new JObject(o.Properties()
                                              .OrderBy(p => p.Name, StringComparer.Ordinal)
                                              .Select(p => new JProperty(p.Name, Canon(p.Value)))),
                    JArray a => new JArray(a.Select(Canon)),
                    _ => t
                };

            var canonicalToken = Canon(token);

            // 3) Minified JSON
            using var sw = new StringWriter();
            using var writer = new JsonTextWriter(sw) { Formatting = Formatting.None };
            canonicalToken.WriteTo(writer);
            writer.Flush();
            return sw.ToString();
        }

        private static string Sha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

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
