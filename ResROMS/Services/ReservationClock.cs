namespace ResROMS.Services;

// The project currently operates in Penang, Malaysia. All form dates/times are
// restaurant-local; database values and audit timestamps are stored in UTC.
// If the restaurant moves to another zone, make this configurable first.
public static class ReservationClock
{
    private static readonly Lazy<TimeZoneInfo> RestaurantZone = new(() =>
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"); }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
        }
    });

    public static DateTime NowLocal => ToLocal(DateTime.UtcNow);

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), RestaurantZone.Value);

    public static DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), RestaurantZone.Value);
}
