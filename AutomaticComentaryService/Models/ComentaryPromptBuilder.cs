using System;
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
        public static string BuildPrompt(ComentaryRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            string action = request.ActionType?.ToUpperInvariant();
            string player = request.Player;
            string position = request.Position;

            switch (action)
            {
                case "ACTIONTYPE.MOVE":
                    return $";{player} moving to {position}.";
                case "ACTIONTYPE.BLOCK":
                    return $";{player} blocking at {position}.";
                case "ACTIONTYPE.BLITZ":
                    return $";{player} blitzing to {position}.";
                case "ACTIONTYPE.FOUL":
                    return $";{player} committing a foul at {position}.";
                case "ACTIONTYPE.PASS":
                    return $";{player} passing the ball to {position}.";
                default:
                    return $"";
            }
        }
        public static string BuildPrompt()
        {
            
            string prompt = string.Empty;
            while (ComentaryQueueModel.Instance.GetMessageCount()>0)
            {
                prompt += BuildPrompt(ComentaryQueueModel.Instance.MessageQueueDequeue());
            }
            return prompt;
        }
        public static string BuildNewGamePrompt()
        {
            return "Write humorus and over the top 2-3 sentence introduction to a match between Human and Dwarf team led by random bots";
        }
    }
}
