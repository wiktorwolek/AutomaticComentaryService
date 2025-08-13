using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace AutomaticComentaryService.Models
{
    public class ComentaryRequest
    {
        [JsonProperty("Action")]
        public Action Action { get; set; }
        [JsonProperty("GameState")]
        public GameState? GameState { get; set; }
    }
    public class Action
    {
        [JsonProperty("action_type")]
        public string ActionType { get; set; }

        [JsonProperty("position")]
        public Position? Position { get; set; }

        [JsonProperty("player_id")]
        public string? PlayerId { get; set; }
    }
}
