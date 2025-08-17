using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using AutomaticComentaryService.Models;

namespace AutomaticComentaryService.Models
{
    public static class CommentaryPromptBuilder
    {
        public static string actionToString(ComentaryRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return JsonSerializer.Serialize(
                request.Action,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                }
            ) + "\n";
        }

        public static List<string> GetRecentActions()
        {
            var actions = new List<string>();
            while (ComentaryQueueModel.Instance.GetMessageCount() > 0)
            {
                actions.Add(actionToString(ComentaryQueueModel.Instance.MessageQueueDequeue()));
            }
            return actions;
        }

        public static string BuildStalingPrompt() => "Nothing happened since last update — output nothing.";

        public static string BuildPrompt(GameState currentGameState, GameState lastGameState,bool isFirstUpdate )
        {
            var currentGameData = JsonSerializer.Serialize(currentGameState, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            var lastGameData = JsonSerializer.Serialize(lastGameState, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var events = TacticalAnalyzer.GenerateTacticalSummary(lastGameState, currentGameState);
            var haveEvents = events.Count > 0;

            // mark high/med priorities to gently bias attention
            // touchdowns/turnovers/injuries -> HIGH, ball/cage/screen changes -> HIGH,
            // other formation/movement context -> MED
            List<string> prioritized = new();
            foreach (var e in events)
            {
                var hi = e.Contains("scored", StringComparison.OrdinalIgnoreCase)
                      || e.Contains("turnover", StringComparison.OrdinalIgnoreCase)
                      || e.Contains("injur", StringComparison.OrdinalIgnoreCase)
                      || e.Contains("ko", StringComparison.OrdinalIgnoreCase)
                      || e.Contains("surf", StringComparison.OrdinalIgnoreCase)
                      || e.Contains("cage", StringComparison.OrdinalIgnoreCase)
                      || e.Contains("screen", StringComparison.OrdinalIgnoreCase)
                      || e.Contains("picked up the ball", StringComparison.OrdinalIgnoreCase)
                      || e.Contains("possession", StringComparison.OrdinalIgnoreCase);
                prioritized.Add($"[{(hi ? "PRIORITY: HIGH" : "PRIORITY: MED")}] {e}");
            }
            var teamA = currentGameState.Teams[0].Name ?? "team a";
            var teamB = currentGameState.Teams[1].Name ?? "team b";
            string tacticalString = haveEvents ? string.Join("\n", prioritized) : "none";
            Console.WriteLine(tacticalString);
            var prompt = $@"
you are a live blood bowl commentator. output 1–3 punchy one-liners about the newest developments only for {teamA} vs {teamB}.

hard rules:
- output only the lines, lowercase; no intros, no meta text, no lists, no markdown.
- 1–3 lines total, max 18 words per line.
- if unsure or nothing notable happened, output nothing.
- use only team and player names found in the current game state.

newest developments priority:
1) tactical events changed this update (cage formed/broken/weakening/strengthening; screen formed/broken; sideline threat; stalling).
2) notable actions: blitz, block result (down/push/surf/ko/cas), pickup, pass+catch, handoff, foul, surf, turnover, ball carrier moved.
3) brief observation about resulting board shape only if it sharpens the line.

blood bowl terms (concise):
- cage = 4 teammates around carrier; call out formed/broken/weak/strengthening; missing corners = leaking/weak.
- screen = defensive line; only say when newly formed or broken.
- surf = pushed off the pitch. turnover = failed action that ends the turn.

tactical events:
{tacticalString}

new actions:
{GetRecentActions()}

current:
{currentGameData}
previous:
{lastGameData}
";

            return prompt.Trim();
        }


        public static string BuildNewGamePrompt() =>
            "Write humorous, over-the-top 2–3 sentence introduction to a match between a Human team and a Dwarf team led by random bots.";

     
    }
}
