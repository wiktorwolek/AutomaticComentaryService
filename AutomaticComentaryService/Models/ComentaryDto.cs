namespace AutomaticComentaryService.Models
{
    public class ComentaryDto
    {
        public string Prompt { get; set; } = string.Empty;
        public string? WhitelistSystem { get; set; }
        public GameState GameState { get; set; } = new();
        public string MatchId { get; set; } = "random";
    }
}