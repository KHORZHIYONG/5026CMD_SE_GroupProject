namespace ResROMS.Services;

// Pure business rules, shared by staff scheduling and system-policy changes.
public static class StaffSchedulingPolicy
{
    public static string? Validate(
        DateTime start, DateTime end,
        IEnumerable<(DateTime Start, DateTime End)> otherShifts,
        SystemSettingsInput settings)
    {
        if (end <= start) return "Shift end must be later than shift start.";
        if ((end - start).TotalHours > settings.MaximumShiftHours)
            return $"Shift exceeds the {settings.MaximumShiftHours}-hour maximum.";

        var others = otherShifts.ToList();
        foreach (var other in others)
        {
            if (start < other.End && end > other.Start)
                return "This staff member has an overlapping shift.";
            var restBefore = (start - other.End).TotalHours;
            var restAfter = (other.Start - end).TotalHours;
            if ((restBefore >= 0 && restBefore < settings.MinimumRestHours) ||
                (restAfter >= 0 && restAfter < settings.MinimumRestHours))
                return $"At least {settings.MinimumRestHours} hours of rest are required between shifts.";
        }

        // Split shifts at Monday midnight so Sunday-night/Monday-morning work
        // counts toward the appropriate calendar week, not only its start week.
        var firstWeek = start.Date.AddDays(-((int)start.DayOfWeek + 6) % 7);
        for (var week = firstWeek; week < end; week = week.AddDays(7))
        {
            var weekEnd = week.AddDays(7);
            double Hours(DateTime a, DateTime b) =>
                Math.Max(0, ( (b < weekEnd ? b : weekEnd) - (a > week ? a : week) ).TotalHours);
            var total = Hours(start, end) + others.Sum(s => Hours(s.Start, s.End));
            if (total > settings.MaximumWeeklyHours + 0.0001)
                return $"Weekly limit of {settings.MaximumWeeklyHours} hours would be exceeded.";
        }
        return null;
    }
}
