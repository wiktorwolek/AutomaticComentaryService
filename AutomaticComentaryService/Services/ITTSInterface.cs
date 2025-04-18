namespace AutomaticComentaryService.Services
{
    public interface ITTSEngine
    {
        public Task<string> GenerateWavAsync(string text);
    }
}
