namespace ResROMS.Services;

/// <summary>
/// Checks that an entire reservation falls within ONE business-day opening window.
/// Overnight hours (e.g. Friday 18:00 -> Saturday 02:00) are supported.
/// Customer-facing reservation times are restaurant-local (Asia/Kuala_Lumpur).
/// </summary>
public static class OpeningHoursPolicy
{
    public static bool IsWithinHours(DateTime startLocal, DateTime endLocal,
        IEnumerable<WeeklyHours> schedule)
    {
        if (endLocal <= startLocal) return false;
        var days = schedule.ToDictionary(h => h.DayOfWeek);

        // Check opening windows starting today and yesterday, since an overnight
        // shift from yesterday might contain a booking early this morning.
        for (int offset = -1; offset <= 0; offset++)
        {
            var businessDate = startLocal.Date.AddDays(offset);
            if (!days.TryGetValue((int)businessDate.DayOfWeek, out var hours) || hours.IsClosed)
                continue;

            var open = businessDate.Add(hours.OpeningTime.ToTimeSpan());
            var close = businessDate.Add(hours.ClosingTime.ToTimeSpan());
            if (hours.ClosingTime < hours.OpeningTime) close = close.AddDays(1);
            if (startLocal >= open && endLocal <= close) return true;
        }
        return false;
    }
}
