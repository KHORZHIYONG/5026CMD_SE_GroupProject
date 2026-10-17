namespace ResROMS.Services;

public sealed record SystemSettingsInput(bool CustomerBookingsEnabled,
    int MinimumBookingNoticeMinutes, int MaximumBookingAdvanceDays);

public interface IAdminSettingsService
{
    Task<SystemSettingsInput> GetAsync(CancellationToken ct = default);
    Task<OperationResult> SaveAsync(SystemSettingsInput input, CancellationToken ct = default);
}
