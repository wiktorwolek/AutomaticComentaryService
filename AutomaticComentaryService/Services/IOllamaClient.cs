using System.Threading;
using System.Threading.Tasks;

namespace AutomaticComentaryService.Services
{
    public interface IOllamaClient
    {

        /// <summary>
        /// Send a user message to a chat session and get the assistant reply.
        /// </summary>
        Task<string> ChatAsync(
            string sessionId,
            string userMessage,
            string whitelistSystem = null,
            string model = "llama3",
           
            CancellationToken ct = default);

        /// <summary>Reset (forget) a chat session.</summary>
        Task ResetSessionAsync(string sessionId);

        
    }
}
