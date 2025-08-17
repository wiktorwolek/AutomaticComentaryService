using System.Xml.Linq;

public static class TacticalAnalyzer
{
    public static List<string> GenerateTacticalSummary(GameState before, GameState after)
    {
        var events = new List<string>();

        // 🔁 TURN / HALF
        if (after.Half != before.Half)
            events.Add($"half_change: new_half={after.Half}");

        if (after.Turn != before.Turn)
            events.Add($"turn_change: new_turn={after.Turn}");

        // 🏈 SCORE
        foreach (var teamAfter in after.Teams ?? new List<Team>())
        {
            var teamBefore = before.Teams?.Find(t => t.TeamId == teamAfter.TeamId);
            if (teamBefore != null && teamAfter.Score > teamBefore.Score)
            {
                var diff = teamAfter.Score - teamBefore.Score;
                events.Add($"score_change: team=\"{teamAfter.Name}\" touchdowns_gained={diff}");
            }
        }

        // 🛡️ CAGE CHANGES
        events.AddRange(DetectCageDiff(before, after));
        events.AddRange(DetectCarrierThreat(before, after));
        // 🧱 SCREEN
        if (!(before.Screen?.HasScreen ?? false) && (after.Screen?.HasScreen ?? false))
            events.Add("screen_status: formed");
        else if ((before.Screen?.HasScreen ?? false) && !(after.Screen?.HasScreen ?? false))
            events.Add("screen_status: broken");

        // 🧍 SIDELINE
        if (!(before.SidelinePressure?.SidelineThreat ?? false) && (after.SidelinePressure?.SidelineThreat ?? false))
            events.Add("sideline_threat: active");

        // ⏳ STALLING
        if (!(before.Stalling?.Stalling ?? false) && (after.Stalling?.Stalling ?? false))
            events.Add("stalling_status: active");
        else if ((before.Stalling?.Stalling ?? false) && !(after.Stalling?.Stalling ?? false))
            events.Add("stalling_status: ended");

        // 🔁 BALL CONTROL
        var beforeCarrier = before.Ball?.Carried;
        var afterCarrier = after.Ball?.Carried;

        if (string.IsNullOrEmpty(beforeCarrier) && !string.IsNullOrEmpty(afterCarrier))
            events.Add($"ball_event: pickup carrier=\"{afterCarrier}\"");
        else if (!string.IsNullOrEmpty(beforeCarrier) && string.IsNullOrEmpty(afterCarrier))
            events.Add($"ball_event: dropped previous_carrier=\"{beforeCarrier}\"");
        else if (!string.IsNullOrEmpty(beforeCarrier) && !string.IsNullOrEmpty(afterCarrier) && beforeCarrier != afterCarrier)
            events.Add($"ball_event: possession_change new_carrier=\"{afterCarrier}\"");

        return events;
    }
    public static IEnumerable<string> DetectCageDiff(GameState before, GameState after)
    {
        var events = new List<string>();

        var b = before?.Cage;
        var a = after?.Cage;

        bool bCaged = b?.IsCaged == true;
        bool aCaged = a?.IsCaged == true;

        // Prefer reliable carrier from Ball, then cage fields
        string? carrierName =
            after?.Ball?.Carried ??
            before?.Ball?.Carried ??
            a?.CarrierName ??
            b?.CarrierName;

        // formed / broken
        if (!bCaged && aCaged)
        {
            var (cx, cy) = (
                a?.CarrierPosition?.ElementAtOrDefault(0) ?? -1,
                a?.CarrierPosition?.ElementAtOrDefault(1) ?? -1
            );
            events.Add($"cage_status: formed carrier=\"{carrierName ?? "unknown"}\" pos=({cx},{cy})");
        }
        else if (bCaged && !aCaged)
        {
            events.Add($"cage_status: broken carrier=\"{carrierName ?? "unknown"}\"");
        }

        // Sets
        var bMissing = ToSetOfTuples(b?.MissingCageCorners);
        var aMissing = ToSetOfTuples(a?.MissingCageCorners);

        var bCorners = ToSetOfTuples(b?.CagePositions);
        var aCorners = ToSetOfTuples(a?.CagePositions);

        // --- Construction change (emit even if not caged) ---
        var restored = bMissing.Except(aMissing).ToList();      // fewer missing ⇒ strengthening
        var newlyMissing = aMissing.Except(bMissing).ToList();  // more missing ⇒ weakening

        if (restored.Count > 0 || newlyMissing.Count > 0)
        {
            if (newlyMissing.Count >= restored.Count && newlyMissing.Count > 0)
                events.Add($"cage_construction: weakened opened_gaps={JoinCoords(newlyMissing)}");
            else if (restored.Count > 0)
                events.Add($"cage_construction: strengthened filled_corners={JoinCoords(restored)}");
        }

        // --- Corner join/left (only makes sense with some cage context) ---
        bool hasCageContext = aCaged || bCaged || aCorners.Count >= 3 || bCorners.Count >= 3;
        if (hasCageContext)
        {
            var joinedCorners = aCorners.Except(bCorners).ToList();
            var leftCorners = bCorners.Except(aCorners).ToList();

            // Resolve carrier team id best-effort; use before for left, after for joined
            var afterCarrierTeam = ResolveCarrierTeamId(after, carrierName) ?? ResolveCarrierTeamId(before, carrierName);
            var beforeCarrierTeam = ResolveCarrierTeamId(before, carrierName) ?? ResolveCarrierTeamId(after, carrierName);

            if (joinedCorners.Count > 0)
            {
                foreach (var (x, y) in joinedCorners)
                {
                    var pname = ResolvePlayerNameAt(after, afterCarrierTeam, x, y)
                                ?? ResolvePlayerNameAt(after, null, x, y);
                    events.Add(!string.IsNullOrEmpty(pname)
                        ? $"cage_corner: joined corner={x},{y} player=\"{pname}\""
                        : $"cage_corner: joined corner={x},{y}");
                }
            }

            if (leftCorners.Count > 0)
            {
                foreach (var (x, y) in leftCorners)
                {
                    var pname = ResolvePlayerNameAt(before, beforeCarrierTeam, x, y)
                                ?? ResolvePlayerNameAt(before, null, x, y);
                    events.Add(!string.IsNullOrEmpty(pname)
                        ? $"cage_corner: left corner={x},{y} player=\"{pname}\""
                        : $"cage_corner: left corner={x},{y}");
                }
            }
        }

        return events;

        // helpers
        static HashSet<(int x, int y)> ToSetOfTuples(List<List<int>>? coords)
            => coords == null
               ? new HashSet<(int, int)>()
               : new HashSet<(int, int)>(coords
                   .Where(p => p != null && p.Count >= 2)
                   .Select(p => (p[0], p[1])));

        static string JoinCoords(IEnumerable<(int x, int y)> pts)
            => string.Join(";", pts.Select(p => $"{p.x},{p.y}"));

        static string? ResolveCarrierTeamId(GameState? gs, string? carrier)
        {
            if (gs?.Teams == null || string.IsNullOrWhiteSpace(carrier)) return null;

            // Prefer exact name
            foreach (var t in gs.Teams)
                if (t.Players != null && t.Players.Any(p => string.Equals(p.Name, carrier, StringComparison.OrdinalIgnoreCase)))
                    return t.TeamId;

            // Fallback: treat as role (e.g., "Thrower")
            foreach (var t in gs.Teams)
                if (t.Players != null && t.Players.Any(p => string.Equals(p.Role, carrier, StringComparison.OrdinalIgnoreCase)))
                    return t.TeamId;

            return null;
        }

        static string? ResolvePlayerNameAt(GameState? gs, string? teamId, int x, int y)
        {
            if (gs?.Teams == null) return null;
            IEnumerable<Player> players = gs.Teams.SelectMany(t => t.Players ?? Enumerable.Empty<Player>());
            if (!string.IsNullOrEmpty(teamId))
                players = gs.Teams.Where(t => t.TeamId == teamId).SelectMany(t => t.Players ?? Enumerable.Empty<Player>());

            foreach (var p in players)
            {
                var px = p.Position?.X;
                var py = p.Position?.Y;
                if (px.HasValue && py.HasValue && px.Value == x && py.Value == y)
                    return p.Name;
            }
            return null;
        }
    }

    public static IEnumerable<string> DetectCarrierThreat(GameState before, GameState after)
    {
        var evts = new List<string>();
        var carrierName = after?.Ball?.Carried ?? before?.Ball?.Carried;
        if (string.IsNullOrWhiteSpace(carrierName)) return evts;

        var (afterTeam, afterPos) = FindCarrierTeamAndPos(after, carrierName);
        var (beforeTeam, beforePos) = FindCarrierTeamAndPos(before, carrierName);
        if (afterTeam == null || afterPos == null) return evts;

        var afterEnemiesAdj = AdjacentEnemyNames(after, afterTeam, afterPos.Value);
        var beforeEnemiesAdj = (beforePos == null)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : AdjacentEnemyNames(before, beforeTeam ?? afterTeam, beforePos.Value);

        var newlyAdj = afterEnemiesAdj.Except(beforeEnemiesAdj, StringComparer.OrdinalIgnoreCase).ToList();
        if (newlyAdj.Count > 0)
        {
            var names = string.Join(",", newlyAdj.Take(3));
            var extra = newlyAdj.Count > 3 ? $"+{newlyAdj.Count - 3} more" : "";
            evts.Add($"attackers_appear: adjacent_enemies={newlyAdj.Count} names={names}{(extra.Length > 0 ? $" ({extra})" : "")}");
        }

        return evts;

        static (string? teamId, (int x, int y)? pos) FindCarrierTeamAndPos(GameState? gs, string carrier)
        {
            if (gs?.Teams == null) return (null, null);
            foreach (var t in gs.Teams)
            {
                var p = t.Players?.FirstOrDefault(pp => string.Equals(pp.Name, carrier, StringComparison.OrdinalIgnoreCase));
                if (p != null)
                {
                    var x = p.Position?.X; var y = p.Position?.Y;
                    return (t.TeamId, (x.HasValue && y.HasValue) ? (x.Value, y.Value) : null);
                }
            }
            return (null, null);
        }

        static HashSet<string> AdjacentEnemyNames(GameState? gs, string? carrierTeamId, (int x, int y) pos)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (gs?.Teams == null) return set;

            foreach (var t in gs.Teams.Where(t => t.TeamId != carrierTeamId))
            {
                foreach (var p in t.Players ?? Enumerable.Empty<Player>())
                {
                    var px = p.Position?.X; var py = p.Position?.Y;
                    if (px.HasValue && py.HasValue &&
                        Math.Max(Math.Abs(px.Value - pos.x), Math.Abs(py.Value - pos.y)) == 1)
                    {
                        if (!string.IsNullOrWhiteSpace(p.Name))
                            set.Add(p.Name);
                    }
                }
            }
            return set;
        }
    }


}
