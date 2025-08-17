using System.Text.RegularExpressions;

public static class CommentaryValidator
{
    private static readonly Regex WordRegex = new(@"\b[\p{L}\p{N}][\p{L}\p{N}'-]*\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CapitalizedSpan = new(@"\b[A-Z][\p{L}’'\-]+(?:\s+[A-Z][\p{L}’'\-]+)*\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Validates a commentary reply against format rules, banned phrases, and whitelists.
    /// </summary>
    public static (bool Ok, string Reason) Validate(
        string reply,
        ISet<string> validTeams,
        ISet<string> validPlayers,
        ISet<string> validRoles,
        ISet<string> bannedPhrases)
    {
        if (reply is null) return (false, "null");
        var lines = reply.Replace("\r", "")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        if (lines.Count is < 1 or > 3) return (false, "lines-count");

        foreach (var line in lines)
        {
            // one sentence heuristic
            var terminals = line.Count(ch => ch is '.' or '!' or '?');
            if (terminals > 1) return (false, "too-many-sentences");

            // ≤ 18 words
            var words = WordRegex.Matches(line).Count;
            if (words > 18) return (false, "too-many-words");

            // no list/meta
            if (line.StartsWith("- ") || line.StartsWith("* ") || char.IsDigit(line.FirstOrDefault()))
                return (false, "listy");

            // banned phrases
            var low = line.ToLowerInvariant();
            if (bannedPhrases.Any(p => low.Contains(p)))
                return (false, "banned-phrase");
        }

        // whitelist: any capitalized span must be a known team/player/role (allow common sentence starters)
        bool IsKnown(string s) =>
            validTeams.Contains(s) || validPlayers.Contains(s) || validRoles.Contains(s);

        var allowStart = new HashSet<string>(StringComparer.Ordinal)
        {
            "The","A","An","They","He","She","It"
        };

        foreach (var line in lines)
        {
            foreach (Match m in CapitalizedSpan.Matches(line))
            {
                var token = m.Value.Trim();
                if (allowStart.Contains(token)) continue;
                if (!IsKnown(token)) return (false, $"unknown-name:{token}");
            }
        }

        return (true, "ok");
    }

    /// <summary>
    /// Attempts to salvage valid lines from a bad reply (max 3).
    /// </summary>
    public static string FilterCleanLines(
        string reply,
        ISet<string> validTeams,
        ISet<string> validPlayers,
        ISet<string> validRoles,
        ISet<string> bannedPhrases)
    {
        var lines = reply.Replace("\r", "")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l));

        var kept = new List<string>();
        foreach (var line in lines)
        {
            var (ok, _) = Validate(line, validTeams, validPlayers, validRoles, bannedPhrases);
            if (ok) kept.Add(line);
            if (kept.Count == 3) break;
        }
        return string.Join("\n", kept);
    }

    /// <summary>
    /// Builds a concise repair prompt to rewrite a bad output.
    /// </summary>
    public static string BuildRepairPrompt(
        string badOutput,
        IEnumerable<string> examples,
        IEnumerable<string> validTeams,
        IEnumerable<string> validPlayers,
        IEnumerable<string> validRoles)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("rewrite the commentary to satisfy ALL rules. output only the final lines, nothing else.");
        sb.AppendLine("- 1–3 lines total; 1 sentence per line; ≤ 18 words per line.");
        sb.AppendLine("- no intros, no meta, no lists, no markdown.");
        sb.AppendLine("- ONLY these names; do not invent others.");
        sb.AppendLine($"teams: {string.Join(", ", validTeams)}");
        sb.AppendLine($"players: {string.Join(", ", validPlayers)}");
        sb.AppendLine($"roles: {string.Join(", ", validRoles)}");
        sb.AppendLine("- if nothing notable happened, output nothing.");
        if (examples.Any())
        {
            sb.AppendLine();
            sb.AppendLine("style examples (do not copy content):");
            foreach (var ex in examples) sb.AppendLine($"- {ex}");
        }
        sb.AppendLine();
        sb.AppendLine("bad output to fix:");
        sb.AppendLine(badOutput.Trim());
        return sb.ToString();
    }
}
