namespace ResROMS.Services;

public sealed record SystemSettingsInput(bool CustomerBookingsEnabled,
    int MinimumBookingNoticeMinutes, int MaximumBookingAdvanceDays,
    int MaximumShiftHours = 12, int MaximumWeeklyHours = 60, int MinimumRestHours = 8);

public interface IAdminSettingsService
{
    Task<SystemSettingsInput> GetAsync(CancellationToken ct = default);
    Task<OperationResult> SaveAsync(SystemSettingsInput input, CancellationToken ct = default);
}
