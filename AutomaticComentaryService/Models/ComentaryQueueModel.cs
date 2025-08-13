using Ollama;

namespace AutomaticComentaryService.Models
{
    public class ComentaryQueueModel
    {
        private static readonly ComentaryQueueModel _instance = new ComentaryQueueModel();
        public static ComentaryQueueModel Instance => _instance;

        private readonly Queue<ComentaryRequest> _messageQueue = new();
        private readonly Queue<string> _promptQueue = new();

        // Dedicated lock objects
        private readonly object _messageLock = new();
        private readonly object _promptLock = new();

        private ComentaryQueueModel() { }
        public void Reset()
        {
            lock (_messageLock)
            {
                _messageQueue.Clear();
                _promptQueue.Clear();
            }
        }
        public void MessageQueueEnqueue(ComentaryRequest message)
        {
            lock (_messageLock)
            {
                _messageQueue.Enqueue(message);
            }
        }

        public ComentaryRequest MessageQueueDequeue()
        {
            lock (_messageLock)
            {
                return _messageQueue.Dequeue();
            }
        }

        public ComentaryRequest MessageQueuePeek()
        {
            lock (_messageLock)
            {
                return _messageQueue.Peek();
            }
        }

        public int GetMessageCount()
        {
            lock (_messageLock)
            {
                return _messageQueue.Count;
            }
        }

        public void PromptQueueEnqueue(string message)
        {
            lock (_promptLock)
            {
                _promptQueue.Enqueue(message);
            }
        }

        public string PromptQueueDequeue()
        {
            lock (_promptLock)
            {
                return _promptQueue.Dequeue();
            }
        }

        public int GetPromptCount()
        {
            lock (_promptLock)
            {
                return _promptQueue.Count;
            }
        }
    }
}
