using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;

namespace ResROMS.Services;

public sealed class AdminSettingsService : IAdminSettingsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AdminSettingsService> _logger;

    public AdminSettingsService(AppDbContext db, ILogger<AdminSettingsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SystemSettingsInput> GetAsync(CancellationToken ct = default)
    {
        var setting = await _db.SystemSettings.AsNoTracking()
            .SingleAsync(s => s.Id == 1, ct);
        return new SystemSettingsInput(setting.CustomerBookingsEnabled,
            setting.MinimumBookingNoticeMinutes, setting.MaximumBookingAdvanceDays);
    }

    public async Task<OperationResult> SaveAsync(SystemSettingsInput input, CancellationToken ct = default)
    {
        if (input.MinimumBookingNoticeMinutes is < 0 or > 1440)
            return new(false, "Minimum booking notice must be between 0 and 1,440 minutes.");
        if (input.MaximumBookingAdvanceDays is < 1 or > 365)
            return new(false, "Maximum booking window must be between 1 and 365 days.");
        if (input.MinimumBookingNoticeMinutes >= input.MaximumBookingAdvanceDays * 1440)
            return new(false, "Minimum notice must be shorter than the advance booking window.");

        try
        {
            var setting = await _db.SystemSettings.SingleAsync(s => s.Id == 1, ct);
            setting.CustomerBookingsEnabled = input.CustomerBookingsEnabled;
            setting.MinimumBookingNoticeMinutes = input.MinimumBookingNoticeMinutes;
            setting.MaximumBookingAdvanceDays = input.MaximumBookingAdvanceDays;
            setting.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new(true, "Reservation policy settings saved successfully.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Saving system settings failed");
            return new(false, "Unable to save settings. Please retry.");
        }
    }
}
