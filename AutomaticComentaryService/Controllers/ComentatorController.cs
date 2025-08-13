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
            LastState = request.GameState;

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
            if(ComentaryQueueModel.Instance.GetMessageCount() == 0)
            {
                prompt = CommentaryPromptBuilder.BuildStalingPrompt();
            }
            else
            {
                prompt = CommentaryPromptBuilder.BuildPrompt(ComentaryController.LastState,ComentaryQueueModel.Instance.MessageQueuePeek().GameState) ;
            }
            _logger.LogDebug(prompt);
            return await CommentateAsync(prompt, matchid.ToString());
        }
        [HttpPost("comentate")]
        public async Task<IActionResult> CommentateAsync(
            [FromBody] string prompt,string matchid = "random",
                CancellationToken ct = default)
        {
            var sessionId = matchid ;

            var commentary = await _ollama.ChatAsync(
                sessionId: sessionId,
                userMessage: prompt,
                model: "llama3:8b",
                ct);
            _logger.LogDebug(commentary);
            var filename = await _tts.GenerateWavAsync(commentary);
            return Ok(new { commentary, audioFile = filename });
        }



    }
}
