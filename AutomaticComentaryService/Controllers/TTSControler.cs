using System.Reflection.Metadata;
using AutomaticComentaryService.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class TTSController : ControllerBase
{
    private readonly ITTSEngine _ttsService;

    public TTSController(ITTSEngine ttsService)
    {
        _ttsService = ttsService;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] TextInput input)
    {
        if (string.IsNullOrWhiteSpace(input?.Text))
            return BadRequest("Text is required.");

        var filename = await _ttsService.GenerateWavAsync(input.Text);
        var filePath = Path.Combine("GeneratedAudio", filename);
        var mimeType = "audio/wav";
        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(fileBytes, mimeType, filename);
    }

    [HttpGet]
    public async Task<IActionResult> Get(string filename)
    {
        var filePath = Path.Combine("GeneratedAudio", filename);
        var mimeType = "audio/wav";
        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(fileBytes, mimeType, filename);
    }
    public class TextInput
    {
        public string Text { get; set; }
    }
}
