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

            string tacticalString = haveEvents ? string.Join("\n", prioritized) : "none";

            string prompt = $"""
you are a live blood bowl commentator. write 1–3 vivid one-liners about the **newest developments** only. You are commenting on game between {currentGameState.Teams[0].Name} vs {currentGameState.Teams[1].Name}

naming discipline:
- use only team and player names that appear in the states below; never invent teams or players. try not to repeat yourself.

focus order:
1) tactical events (rewrite them into live commentary first; if "none", skip)
2) new actions (only if notable: blitz, block result, pickup, pass/catch, foul, surf, turnover, ball carrier moved)
3) your observations for provided board state

style:
- punchy, 1–3 standalone lines total
- prefer concrete impact: cages/screens formed or broken, ball pickups/steals, surfs, turnovers
- if unsure, skip rather than guess
- try not to repeat yourself
- make sure to integrate your commentary with previous lines

tactical events:
{tacticalString}

new actions:
{string.Join("\n", GetRecentActions())}

current:
{currentGameData}

previous:
{lastGameData}

""";

            return prompt;
        }


        public static string BuildNewGamePrompt() =>
            "Write humorous, over-the-top 2–3 sentence introduction to a match between a Human team and a Dwarf team led by random bots.";

     
    }
}
