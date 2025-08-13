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
        var beforeCage = before.Cage;
        var afterCage = after.Cage;

        if (!(beforeCage?.IsCaged ?? false) && (afterCage?.IsCaged ?? false))
        {
            var name = afterCage?.CarrierName ?? "unknown";
            var pos = afterCage?.CarrierPosition;
            events.Add($"cage_status: formed carrier=\"{name}\" pos=({pos?[0]},{pos?[1]})");
        }
        else if ((beforeCage?.IsCaged ?? false) && !(afterCage?.IsCaged ?? false))
        {
            var name = beforeCage?.CarrierName ?? "unknown";
            events.Add($"cage_status: broken carrier=\"{name}\"");
        }

        // 🧱 CAGE CORNER UPDATES
        var prevMissing = new HashSet<string>(beforeCage?.MissingCageCorners?.Select(p => $"{p[0]},{p[1]}") ?? []);
        var newMissing = new HashSet<string>(afterCage?.MissingCageCorners?.Select(p => $"{p[0]},{p[1]}") ?? []);

        var addedMissing = newMissing.Except(prevMissing).ToList();
        var removedMissing = prevMissing.Except(newMissing).ToList();

        if (addedMissing.Count > removedMissing.Count)
            events.Add($"cage_construction: weakening new_missing_corners={string.Join(";", addedMissing)} check game state for player information");
        else if (addedMissing.Count < removedMissing.Count)
            events.Add($"cage_construction: strengthening restored_corners={string.Join(";", removedMissing)} check game state for player information");

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

    private static string Ordinal(int number)
    {
        return number switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            _ => number + "th"
        };
    }
}
