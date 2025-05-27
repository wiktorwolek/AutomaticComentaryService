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
        {   ComentaryQueueModel.Instance.MessageQueueEnqueue(request, 1);
            if(request.ActionType?.ToUpperInvariant()=="ACTIONTYPE.END_TURN")
            {
                ComentaryQueueModel.Instance.PromptQueueEnqueue(CommentaryPromptBuilder.BuildPrompt());
            }
            return Ok();
        }

        [HttpPost("AddNewGame")]
        public IActionResult AddNewGame(CancellationToken cancellationToken = default)
        {
            string prompt = AutomaticComentaryService.Models.CommentaryPromptBuilder.BuildNewGamePrompt();
            if (string.IsNullOrEmpty(prompt))
                return Ok();
            ComentaryQueueModel.Instance.PromptQueueEnqueue(prompt, 1);
            return Ok();
        }

        [HttpGet("GetLast")]
        public async Task<IActionResult> Get()
        {
            if(ComentaryQueueModel.Instance.GetPromptCount() == 0)
            {
                return NoContent();
            }
           return await CommentateAsync(ComentaryQueueModel.Instance.PromptQueueDequeue());
        }


       
    }
}
