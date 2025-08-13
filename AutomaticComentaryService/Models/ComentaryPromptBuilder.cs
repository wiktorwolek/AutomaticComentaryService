using System;
using System.Text.Json;
using AutomaticComentaryService.Models;
using Microsoft.Extensions.Primitives;
namespace AutomaticComentaryService.Models
{
    public static class CommentaryPromptBuilder
    {
        /// <summary>
        /// Builds a user prompt instructing the LLM to generate a short, punchy, humorous commentary line
        /// for a given game action.
        /// </summary>
        /// <param name="request">An object containing ActionType, Player, and Position.</param>
        /// <returns>An instruction-style prompt string describing the event and asking for a commentary line.</returns>
        /// <remarks>
        /// Supported ActionType values: MOVE, BLOCK, BLITZ, FOUL, PASS. Other types use a generic fallback.
        /// </remarks>
        public static string actionToString(ComentaryRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var action = JsonSerializer.Serialize(request.Action, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var prompt = $"{action}\n";
        
            return prompt;
        }
        public static string BuildStalingPrompt()
        {
            return "Nothing happend since last update stall for time";
        }
        public static string BuildPrompt(GameState currentGameState, GameState lastGameState)
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
            string tacticalString = "";
            if (events.Count == 0)
                tacticalString="none";
            else
                tacticalString= string.Join("\n", events);
            string prompt = $"""

📋 TACTICAL EVENTS (always comment on these first — ONLY if they are listed):


{tacticalString}

---

🆕 ADDITIONAL NEW ACTIONS (comment on these only if present, and only after Tactical Events):
{string.Join("\n", GetRecentActions())}

---
Previous board state:
{lastGameData}
Current board state:
{currentGameData}

📦 CONTEXT (board state after latest actions):

Rules:
- Write exactly 1–3 short, dramatic one-liners — NO more.
- First, turn the tactical events above into live commentary before considering other actions.
- No greetings, recaps, or crowd/weather talk.
- Always reflect tactical events if present.
- Never hallucinate touchdowns or injuries.
- Do not repeat commentary from prior turns.
- If there is no relevant event in Tactical Events or New Actions, output nothing.
- Only describe events that explicitly appear in Tactical Events or New Actions above.
- If you are unsure, output nothing instead of guessing.
- Tactical Events are factual summaries — rephrase them into colorful, dramatic one-liners.
- Do not copy Tactical Events word-for-word. Always rewrite in your own style.
- Use the facts, but turn them into lively commentary.
❗ If no valid commentary target exists in Tactical Events or New Actions, return an empty string.
❗ OUTPUT POLICY:
Under no circumstances may you output anything except the commentary lines themselves. 
No intros, no explanations, no lists, no formatting, no markdown, no "Here is the commentary". 
If unsure, output nothing rather than meta text.
""";

            return prompt;
        }

        public static string BuildNewGamePrompt()
        {
            return "Write humorus and over the top 2-3 sentence introduction to a match between Human and Dwarf team led by random bots";
        }
        public static List<string> GetRecentActions()
        {
            var actions = new List<string>();
            while (ComentaryQueueModel.Instance.GetMessageCount() > 0)
            {
               actions.Add( actionToString(ComentaryQueueModel.Instance.MessageQueueDequeue()));
            }
            return actions;
        }

    }
}
