namespace AutomaticComentaryService.Models
{
   
        public class ComentaryQueueModel
        {
            private static readonly ComentaryQueueModel _instance = new ComentaryQueueModel();

            public static ComentaryQueueModel Instance => _instance;

            private PriorityQueue<ComentaryRequest, int> MessageQueue { get; } = new PriorityQueue<ComentaryRequest, int>();
            private PriorityQueue<string, int> PromptQueue { get; } = new PriorityQueue<string, int>();

            private ComentaryQueueModel() { }

            public ComentaryRequest MessageQueueDequeue()
            {
            lock (ComentaryQueueModel.Instance)
            {
                return Instance.MessageQueue.Dequeue();
            }
            }
            public  void MessageQueueEnqueue(ComentaryRequest message, int priority=1)
            {
            lock (Instance.MessageQueue)
            {
                Instance.MessageQueue.Enqueue(message, priority);
            }
            }
            public  string PromptQueueDequeue()
            {
                lock (ComentaryQueueModel.Instance)
                {
                    return Instance.PromptQueue.Dequeue();
                }
            }
            public  void PromptQueueEnqueue(string message, int priority = 1)
            {
                lock (Instance.PromptQueue)
                {
                    Instance.PromptQueue.Enqueue(message, priority);
                }
            }
            public  int GetPromptCount()
            {
                return Instance.PromptQueue.Count;
            }
            public int GetMessageCount()
            {
                return Instance.MessageQueue.Count;
            }
    }
    
}
