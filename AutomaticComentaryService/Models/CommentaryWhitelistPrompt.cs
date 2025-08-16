using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

public static class CommentaryWhitelistPrompt
{
    public static string Build(GameState state, IEnumerable<string>? extraBannedPhrases = null)
    {
        var teams = (state?.Teams ?? new List<Team>())
            .Select(t => SafeLower(t?.Name))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        var players = (state?.Teams ?? new List<Team>())
            .SelectMany(t => t?.Players ?? new List<Player>())
            .Select(p => SafeLower(p?.Name))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        // Optional: include roles as a soft hint (model may use them, but won’t invent names)
        var roles = (state?.Teams ?? new List<Team>())
            .SelectMany(t => t?.Players ?? new List<Player>())
            .Select(p => SafeLower(p?.Role))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        // Banned filler that often triggers “broadcaster voice”
        var banned = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "folks", "welcome", "stay tuned", "halftime", "pregame", "postgame"
        };
        if (extraBannedPhrases != null)
        {
            foreach (var b in extraBannedPhrases)
            {
                var s = SafeLower(b);
                if (!string.IsNullOrWhiteSpace(s)) banned.Add(s);
            }
        }

        // Serialize with System.Text.Json to ensure proper escaping
        string TeamsJson = JsonSerializer.Serialize(teams);
        string PlayersJson = JsonSerializer.Serialize(players);
        string RolesJson = JsonSerializer.Serialize(roles);
        string BannedJson = JsonSerializer.Serialize(banned.Select(SafeLower).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s));

        var sb = new StringBuilder();
        sb.AppendLine("whitelist and output gate:");
        sb.Append("VALID_TEAMS: ").AppendLine(TeamsJson);
        sb.Append("VALID_PLAYERS: ").AppendLine(PlayersJson);
        sb.Append("VALID_ROLES: ").AppendLine(RolesJson);
        sb.Append("BANNED_PHRASES: ").AppendLine(BannedJson);

        // Short, strict tail rules (keep this compact so the model can’t miss it)
        sb.AppendLine("rules:");
        sb.AppendLine("- output 1–3 lines, each ≤ 18 words, all lowercase.");
        sb.AppendLine("- use only names from VALID_TEAMS and VALID_PLAYERS; if unsure, output nothing.");
        sb.AppendLine("- no intros, no summaries, no greetings, no crowd hype, no banned phrases.");
        sb.AppendLine("- comment only on newest events; do not repeat prior highlights.");
        sb.AppendLine("###"); // handy sentinel; also add \"###\" to your stop list

        return sb.ToString();
    }

    private static string SafeLower(string? s) => s?.Trim().ToLowerInvariant() ?? string.Empty;
}
