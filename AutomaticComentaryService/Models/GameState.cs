using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

public class GameState
{
    [JsonProperty("half")]
    public int Half { get; set; }

    [JsonProperty("turn")]
    public int Turn { get; set; }

    [JsonProperty("weather")]
    public string? Weather { get; set; }

    [JsonProperty("ball")]
    public Ball? Ball { get; set; }

    [JsonProperty("teams")]
    public List<Team>? Teams { get; set; }

    [JsonProperty("current_team")]
    public string? CurrentTeam { get; set; }

    [JsonProperty("game_over")]
    public bool GameOver { get; set; }

    // 🔽 New Tactical Features 🔽
    [JsonProperty("cage")]
    public CageInfo? Cage { get; set; }

    [JsonProperty("screen")]
    public ScreenInfo? Screen { get; set; }

    [JsonProperty("sideline_pressure")]
    public SidelinePressureInfo? SidelinePressure { get; set; }

    [JsonProperty("stalling")]
    public StallingInfo? Stalling { get; set; }
}

public class Ball
{
    [JsonProperty("position")]
    public Position? Position { get; set; }

    [JsonProperty("on_ground")]
    public bool OnGround { get; set; }

    [JsonProperty("carried")]
    public string? Carried { get; set; }
}

public class Team
{
    [JsonProperty("team_id")]
    public string? TeamId { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("score")]
    public int Score { get; set; }

    [JsonProperty("turn")]
    public int Turn { get; set; }

    [JsonProperty("rerolls")]
    public int Rerolls { get; set; }

    [JsonProperty("players")]
    public List<Player>? Players { get; set; }
}

public class Player
{
    [JsonProperty("player_id")]
    public string? PlayerId { get; set; }

    [JsonProperty("position")]
    public Position? Position { get; set; }

    [JsonProperty("team")]
    public string? Team { get; set; }

    [JsonProperty("role")]
    public string? Role { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("skills")]
    public List<string>? Skills { get; set; }

    [JsonProperty("state")]
    public PlayerState? State { get; set; }
}

public class Position
{
    [JsonProperty("x")]
    public int? X { get; set; }

    [JsonProperty("y")]
    public int? Y { get; set; }
}

public class PlayerState
{
    [JsonProperty("stunned")]
    public bool Stunned { get; set; }

    [JsonProperty("used")]
    public bool Used { get; set; }

    [JsonProperty("has_ball")]
    public bool HasBall { get; set; }

    [JsonProperty("up")]
    public bool Up { get; set; }
}

public class CageInfo
{
    [JsonProperty("cage_present")]
    public bool IsCaged { get; set; }

    [JsonProperty("carrier_name")]
    public string? CarrierName { get; set; }
    [JsonProperty("reason")]
    public string? Reason { get; set; }

    [JsonProperty("carrier_position")]
    public List<int>? CarrierPosition { get; set; }

    [JsonProperty("cage_positions")]
    public List<List<int>>? CagePositions { get; set; }

    [JsonProperty("missing_cage_corners")]
    public List<List<int>>? MissingCageCorners { get; set; }
}

public class ScreenInfo
{
    [JsonProperty("has_screen")]
    public bool HasScreen { get; set; }

    [JsonProperty("screeners")]
    public List<List<int>>? Screeners { get; set; }
}

public class SidelinePressureInfo
{
    [JsonProperty("sideline_threat")]
    public bool SidelineThreat { get; set; }

    [JsonProperty("near_sideline")]
    public bool NearSideline { get; set; }

    [JsonProperty("defenders_nearby")]
    public List<NearbyDefender>? DefendersNearby { get; set; }
}

public class NearbyDefender
{
    [JsonProperty("role")]
    public string? Role { get; set; }

    [JsonProperty("position")]
    public List<int>? Position { get; set; }
}

public class StallingInfo
{
    [JsonProperty("stalling")]
    public bool Stalling { get; set; }

    [JsonProperty("near_endzone")]
    public bool NearEndzone { get; set; }

    [JsonProperty("supporting_teammates")]
    public List<NearbyDefender>? SupportingTeammates { get; set; }
}
