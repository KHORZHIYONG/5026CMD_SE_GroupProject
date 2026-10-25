namespace ResROMS.Services;

public sealed record StaffSummary(int Id, string FullName, string Role);
public sealed record ScheduledShift(int Id, int StaffUserId, string FullName,
    string Role, DateTime StartLocal, DateTime EndLocal, string Notes);
public sealed record StaffWeek(DateOnly WeekStart,
    IReadOnlyList<StaffSummary> Staff, IReadOnlyList<ScheduledShift> Shifts,
    IReadOnlyList<WeeklyHours> OpeningHours, SystemSettingsInput Policy);

public interface IStaffSchedulingService
{
    Task<StaffWeek> GetWeekAsync(DateOnly weekStart, CancellationToken ct = default);
    Task<OperationResult> SaveAsync(int shiftId, int staffId, DateTime startLocal,
        DateTime endLocal, string? notes, CancellationToken ct = default);
    Task<OperationResult> MoveAsync(int shiftId, DateOnly newDate, CancellationToken ct = default);
    Task<OperationResult> DeleteAsync(int shiftId, CancellationToken ct = default);
}
