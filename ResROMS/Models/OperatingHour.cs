namespace ResROMS.Models;

// One row per day, DayOfWeek: Sunday=0, Monday=1, ..., Saturday=6.
// If ClosingTime is before OpeningTime the restaurant closes the NEXT day.
// Equal opening and closing times are not permitted on open days.
public sealed class OperatingHour
{
    public int DayOfWeek { get; set; }
    public bool IsClosed { get; set; }
    public TimeOnly OpeningTime { get; set; } = new(9, 0);
    public TimeOnly ClosingTime { get; set; } = new(22, 0);
}
