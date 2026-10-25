using Microsoft.EntityFrameworkCore;
using System.Data;
using Microsoft.Data.SqlClient;
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
            setting.MinimumBookingNoticeMinutes, setting.MaximumBookingAdvanceDays,
            setting.MaximumShiftHours, setting.MaximumWeeklyHours, setting.MinimumRestHours);
    }

    public async Task<OperationResult> SaveAsync(SystemSettingsInput input, CancellationToken ct = default)
    {
        if (input.MinimumBookingNoticeMinutes is < 0 or > 1440)
            return new(false, "Minimum booking notice must be between 0 and 1,440 minutes.");
        if (input.MaximumBookingAdvanceDays is < 1 or > 365)
            return new(false, "Maximum booking window must be between 1 and 365 days.");
        if (input.MinimumBookingNoticeMinutes >= input.MaximumBookingAdvanceDays * 1440)
            return new(false, "Minimum notice must be shorter than the advance booking window.");

        if (input.MaximumShiftHours is < 1 or > 16 || input.MaximumWeeklyHours is < 8 or > 80 ||
            input.MinimumRestHours is < 0 or > 24)
            return new(false, "Check scheduling limits: shifts 1–16h, weekly 8–80h, rest 0–24h.");
        if (input.MaximumShiftHours > input.MaximumWeeklyHours)
            return new(false, "Weekly hours must be at least as large as a single shift maximum.");

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            _ = await _db.RestaurantProfiles.FromSqlInterpolated(
                $"SELECT * FROM dbo.RestaurantProfiles WITH (UPDLOCK, HOLDLOCK) WHERE Id = {1}")
                .SingleAsync(ct);

            // Reject stricter scheduling rules when they would invalidate a future roster.
            var nowLocal = ReservationClock.ToLocal(DateTime.UtcNow);
            var all = await _db.StaffShifts.AsNoTracking()
                .Where(s => s.EndLocal > nowLocal.AddDays(-8))
                .Select(s => new { s.Id, s.StaffUserId, s.StartLocal, s.EndLocal }).ToListAsync(ct);
            foreach (var member in all.GroupBy(s => s.StaffUserId))
            foreach (var shift in member.Where(s => s.EndLocal > nowLocal))
            {
                var issue = StaffSchedulingPolicy.Validate(shift.StartLocal, shift.EndLocal,
                    member.Where(s => s.Id != shift.Id)
                        .Select(s => (s.StartLocal, s.EndLocal)), input);
                if (issue is not null)
                    return new(false, $"Existing shift #{shift.Id} conflicts with the proposed policy: {issue}");
            }
            var setting = await _db.SystemSettings.SingleAsync(s => s.Id == 1, ct);
            setting.CustomerBookingsEnabled = input.CustomerBookingsEnabled;
            setting.MinimumBookingNoticeMinutes = input.MinimumBookingNoticeMinutes;
            setting.MaximumBookingAdvanceDays = input.MaximumBookingAdvanceDays;
            setting.MaximumShiftHours = input.MaximumShiftHours;
            setting.MaximumWeeklyHours = input.MaximumWeeklyHours;
            setting.MinimumRestHours = input.MinimumRestHours;
            setting.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(true, "Reservation and staff scheduling settings saved successfully.");
        }
        catch (SqlException ex) when (ex.Number is 1205 or 1222)
        {
            _logger.LogWarning(ex, "Settings update lock conflict");
            return new(false, "Restaurant settings changed during the update. Please retry.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Saving system settings failed");
            return new(false, "Unable to save settings. Please retry.");
        }
    }
}
