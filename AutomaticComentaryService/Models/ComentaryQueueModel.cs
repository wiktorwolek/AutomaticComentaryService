namespace AutomaticComentaryService.Models
{
   
        public class ComentaryQueueModel
        {
            private static readonly ComentaryQueueModel _instance = new ComentaryQueueModel();

            public static ComentaryQueueModel Instance => _instance;

            public PriorityQueue<string, int> MessageQueue { get; } = new PriorityQueue<string, int>();

            private ComentaryQueueModel() { }

            public static string Dequeue()
            {
            lock (ComentaryQueueModel.Instance)
            {
                return Instance.MessageQueue.Dequeue();
            }
            }
            public static void Enqueue(string message, int priority=1)
            {
            lock (Instance.MessageQueue)
            {
                Instance.MessageQueue.Enqueue(message, priority);
            }
            }
        }
    
}
