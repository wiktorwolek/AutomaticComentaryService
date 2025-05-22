using System.ComponentModel;
using AutomaticComentaryService.Models;
using AutomaticComentaryService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutomaticComentaryService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ComentaryController : ControllerBase
    {
        private readonly IOllamaClient _ollama;
        private readonly ITTSEngine _tts;

        private readonly string _systemPrompt = """
You are a professional, high-energy Blood Bowl commentator known for short, punchy, and dramatic one-liners.

Your job is to describe game events with flair. Always follow these rules:
1. Comment ONLY on the specific event.
2. Use exciting, over-the-top sports metaphors.
3. Include 1–2 of these key Blood Bowl terms: "BLITZ", "POW", "CAS", "GFI", "TOUCHDOWN", "BLOCK", "DODGE", "FOUL".
4. Keep responses under 25 words. Brevity = excitement.
5. Speak like a TV sports announcer hyping up a wild moment.

Format:
### EVENT: <brief description of what happened>
### COMMENTARY: "<your wild and vivid one-liner>"

Examples:
### EVENT: Orc Blitzer POWs Human Thrower  
### COMMENTARY: "BRUTAL HIT! The Thrower FLIPS like a pancake – textbook POW!"

### EVENT: Elf fails Dodge roll  
### COMMENTARY: "ELF DOWN! He gambled on grace and paid in faceplants!"
""";


        public ComentaryController(IOllamaClient ollama, ITTSEngine tts)
        {
            _ollama = ollama;
            _tts = tts;
        }

        [HttpPost("comentate")]
        public async Task<IActionResult> CommentateAsync([FromBody] string prompt, CancellationToken cancellationToken = default)
        {
            var commentary = await _ollama.GenerateCompletionAsync(prompt, model: "blood-bowl-comentator-model", cancellationToken);
            Console.WriteLine(commentary);
            var filename = await _tts.GenerateWavAsync(commentary);
            return Ok(new { commentary, audioFile = filename });
        }
        [HttpPost("AddEvent")]
        public IActionResult AddEvent([FromBody] ComentaryRequest request, CancellationToken cancellationToken = default)
        {
            string prompt = AutomaticComentaryService.Models.CommentaryPromptBuilder.BuildPrompt(request);
            if (string.IsNullOrEmpty(prompt))
                return Ok();
            ComentaryQueueModel.Enqueue(prompt, 1);
            return Ok();
        }

        [HttpPost("AddNewGame")]
        public IActionResult AddNewGame(CancellationToken cancellationToken = default)
        {
            string prompt = AutomaticComentaryService.Models.CommentaryPromptBuilder.BuildNewGamePrompt();
            if (string.IsNullOrEmpty(prompt))
                return Ok();
            ComentaryQueueModel.Enqueue(prompt, 1);
            return Ok();
        }

        [HttpGet("GetLast")]
        public async Task<IActionResult> Get()
        {
            if(ComentaryQueueModel.Instance.MessageQueue.Count == 0)
            {
                return NoContent();
            }
           return await CommentateAsync(ComentaryQueueModel.Dequeue());
        }


       
    }
}
