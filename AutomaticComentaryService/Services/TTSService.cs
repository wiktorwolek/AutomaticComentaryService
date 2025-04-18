using System.Diagnostics;
using AutomaticComentaryService.Services;

public class TTSService : ITTSEngine
{
    private readonly ILogger<TTSService> _logger;
    private readonly string _pythonScriptPath = "Scripts/generate_wav.py"; // adjust path
    private readonly string _pythonExePath = "python"; // or full path to python

    public TTSService(ILogger<TTSService> logger)
    {
        _logger = logger;
    }

    public async Task<string> GenerateWavAsync(string text)
    {
        var outputFileName = $"output_{Guid.NewGuid()}.wav";
        var outputFilePath = Path.Combine("GeneratedAudio", outputFileName);

        Directory.CreateDirectory("GeneratedAudio");

        var psi = new ProcessStartInfo
        {
            FileName = _pythonExePath,
            ArgumentList = { _pythonScriptPath, text, outputFilePath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError("TTS generation failed: {0}", stderr);
            throw new Exception("TTS generation failed.");
        }

        return outputFileName;
    }
}
