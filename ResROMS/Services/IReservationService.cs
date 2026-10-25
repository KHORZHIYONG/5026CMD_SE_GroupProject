namespace ResROMS.Services;

public sealed record ReservationRequest(int TableId, DateTime StartLocal, int DurationMinutes,
    int PartySize, string? SpecialRequests);
public sealed record ReservationResult(bool Success, string Message);
public sealed record ReservationSummary(int Id, int CustomerId, string CustomerName, string CustomerEmail,
    int TableId, string TableNumber, int PartySize, DateTime StartLocal, DateTime EndLocal,
    string Status, string SpecialRequests);
public sealed record ReservationCustomerOption(int Id, string Name, string Email);
public sealed record ReservationTableOption(int Id, string TableNumber, int Capacity, string AreaName, string Status);

public sealed record TableAvailabilityResult(string? Error, IReadOnlyList<ReservationTableOption> Tables);

public interface IReservationService
{
    Task<IReadOnlyList<ReservationCustomerOption>> GetCustomersAsync();
    Task<IReadOnlyList<ReservationTableOption>> GetTablesAsync();
    Task<TableAvailabilityResult> CheckAvailabilityAsync(DateTime startLocal, int durationMinutes,
        int partySize, bool forCustomer = false);
    Task<IReadOnlyList<ReservationTableOption>> FindAvailableTablesAsync(DateTime startLocal,
        int durationMinutes, int partySize, bool forCustomer = false);
    Task<IReadOnlyList<ReservationSummary>> GetForAdminAsync(DateTime? dateLocal);
    Task<IReadOnlyList<ReservationSummary>> GetForCustomerAsync(int customerId);
    Task<ReservationSummary?> FindAsync(int id, int? customerId = null);
    Task<ReservationResult> CreateForAdminAsync(int customerId, ReservationRequest input);
    Task<ReservationResult> CreateForCustomerAsync(int customerId, ReservationRequest input);
    Task<ReservationResult> UpdateAsync(int reservationId, int? actingCustomerId, ReservationRequest input);
    Task<ReservationResult> ConfirmAsync(int reservationId);
    Task<ReservationResult> CompleteAsync(int reservationId);
    Task<ReservationResult> CancelAsync(int reservationId, int? actingCustomerId);
}
