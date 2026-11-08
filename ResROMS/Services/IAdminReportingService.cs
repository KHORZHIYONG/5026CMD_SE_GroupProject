namespace ResROMS.Services;

public sealed record ReportRange(DateOnly From, DateOnly To, string GroupBy, int? CategoryId = null);
public sealed record ReportTrend(string Label, decimal NetCollected, int Reservations, int Orders);
public sealed record MenuPopularity(int MenuItemId, string Name, int Quantity, decimal OrderValue);
public sealed record StatusBreakdown(string Status, int Count);
public sealed record UpcomingBooking(int Id, DateTime StartLocal, string TableNumber, string CustomerName, int PartySize, string Status);

public sealed record AdminReport(
    ReportRange Range,
    int Reservations,
    int PendingReservations,
    int CancelledReservations,
    int Orders,
    decimal OrderValue,
    decimal CollectedPayments,
    decimal RecordedRefunds,
    decimal NetCollected,
    int NewCustomers,
    int ActiveCustomers,
    IReadOnlyList<ReportTrend> Trend,
    IReadOnlyList<MenuPopularity> PopularItems,
    IReadOnlyList<StatusBreakdown> ReservationStatuses);

public sealed record AdminDashboard(
    int TodayReservations,
    int ActiveOrders,
    decimal TodayNetCollected,
    IReadOnlyList<ReportTrend> LastSevenDays,
    IReadOnlyList<StatusBreakdown> TableStatuses,
    IReadOnlyList<MenuPopularity> PopularItems,
    IReadOnlyList<UpcomingBooking> UpcomingBookings);

public interface IAdminReportingService
{
    Task<AdminDashboard> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<AdminReport> GetReportAsync(ReportRange range, CancellationToken cancellationToken = default);
}
