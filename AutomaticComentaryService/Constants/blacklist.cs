namespace AutomaticComentaryService.Constants
{
    public static class BannedPhrases
    {
        // Immutable array of phrases (easy to pass to Ollama "stop" if you choose)
        public static readonly string[] Items = new[]
        {
            "```",
            "```python",
            "here are",
            "based on the provided",
            "the provided json",
            "to answer your question",
            "in summary",
            "welcome",
            "folks",
            "we're live",
            "stay tuned",
            "here are the commentary lines",
            "the ball is snapped"
        };

        public static readonly ISet<string> Set =
            new HashSet<string>(Items, StringComparer.OrdinalIgnoreCase);
    }
}