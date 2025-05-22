using System;
using AutomaticComentaryService.Models;
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
        public static string BuildPrompt(ComentaryRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            string action = request.ActionType?.ToUpperInvariant();
            string player = request.Player;
            string position = request.Position;

            switch (action)
            {
                case "ACTIONTYPE.MOVE":
                    return $"Write one short, punchy, humorous, and dramatic commentary line describing {player} moving to {position}.";
                case "ACTIONTYPE.BLOCK":
                    return $"Write one short, punchy, humorous, and dramatic commentary line describing {player} blocking at {position}.";
                case "ACTIONTYPE.BLITZ":
                    return $"Write one short, punchy, humorous, and dramatic commentary line describing {player} blitzing to {position}.";
                case "ACTIONTYPE.FOUL":
                    return $"Write one short, punchy, humorous, and dramatic commentary line describing {player} committing a foul at {position}.";
                case "ACTIONTYPE.PASS":
                    return $"Write one short, punchy, humorous, and dramatic commentary line describing {player} passing the ball to {position}.";
                default:
                    return $"";
            }
        }
        public static string BuildNewGamePrompt()
        {
            return "Write humorus and over the top 2-3 sentence introduction to a match between Human and Dwarf team led by random bots";
        }
    }
}
