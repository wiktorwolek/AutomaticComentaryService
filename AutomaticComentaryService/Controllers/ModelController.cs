using AutomaticComentaryService.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AutomaticComentaryService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModelController : ControllerBase
    {
        private readonly IOllamaClient _ollamaClient;

        public ModelController(IOllamaClient ollamaClient)
        {
            _ollamaClient = ollamaClient;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] GenerateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt) || string.IsNullOrWhiteSpace(request.Model))
                return BadRequest("Both prompt and model are required.");

            var response = await _ollamaClient.GenerateCompletionAsync(
                request.Prompt,
                request.Model
            );

            return Ok(new { response });
        }
    }

    public class GenerateRequest
    {
        public string Prompt { get; set; }
        public string Model { get; set; }
    }
}
