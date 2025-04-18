using System.Threading;
using System.Threading.Tasks;

namespace AutomaticComentaryService.Services
{
    public interface IOllamaClient
    {
        /// <summary>
        /// Generates a text completion from the given prompt.
        /// </summary>
        /// <param name="prompt">The input prompt text.</param>
        /// <param name="model">The model name (default "llama2").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Completed text from the model.</returns>
        Task<string> GenerateCompletionAsync(string prompt, string model = "llama2", CancellationToken cancellationToken = default);
    }
}
