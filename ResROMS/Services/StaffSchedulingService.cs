using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;

namespace ResROMS.Services;

public sealed class StaffSchedulingService : IStaffSchedulingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<StaffSchedulingService> _log;

    public StaffSchedulingService(AppDbContext db, ILogger<StaffSchedulingService> log)
    { _db = db; _log = log; }

    public async Task<StaffWeek> GetWeekAsync(DateOnly weekStart, CancellationToken ct = default)
    {
        var monday = MondayOf(weekStart);
        var from = monday.ToDateTime(TimeOnly.MinValue);
        var until = from.AddDays(7);
        var people = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && (u.Role == "Waiter" || u.Role == "Kitchen Staff"))
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.Role }).ToListAsync(ct);
        var rows = await _db.StaffShifts.AsNoTracking()
            .Where(s => s.StartLocal < until && s.EndLocal > from)
            .OrderBy(s => s.StartLocal)
            .Select(s => new { s.Id, s.StaffUserId, s.StaffUser.FullName,
                s.StaffUser.Role, s.StartLocal, s.EndLocal, s.Notes }).ToListAsync(ct);
        var hours = await _db.OperatingHours.AsNoTracking()
            .OrderBy(h => h.DayOfWeek)
            .Select(h => new WeeklyHours(h.DayOfWeek, h.IsClosed, h.OpeningTime, h.ClosingTime))
            .ToListAsync(ct);
        var policy = await _db.SystemSettings.AsNoTracking()
            .SingleAsync(s => s.Id == 1, ct);
        return new StaffWeek(monday,
            people.Select(p => new StaffSummary(p.Id, p.FullName, p.Role)).ToList(),
            rows.Select(r => new ScheduledShift(r.Id, r.StaffUserId, r.FullName,
                r.Role, r.StartLocal, r.EndLocal, r.Notes)).ToList(),
            hours, new SystemSettingsInput(policy.CustomerBookingsEnabled,
                policy.MinimumBookingNoticeMinutes, policy.MaximumBookingAdvanceDays,
                policy.MaximumShiftHours, policy.MaximumWeeklyHours, policy.MinimumRestHours));
    }

    public async Task<OperationResult> SaveAsync(int shiftId, int staffId, DateTime startLocal,
        DateTime endLocal, string? notes, CancellationToken ct = default)
    {
        if (staffId <= 0) return Fail("Select a staff member.");
        if (startLocal.Year < 2020 || endLocal.Year > 2100)
            return Fail("Choose a date between 2020 and 2100.");
        if (endLocal <= startLocal || (endLocal - startLocal).TotalHours > 24)
            return Fail("Shift duration must be greater than zero and no longer than 24 hours.");
        var cleanNotes = (notes ?? "").Trim();
        if (cleanNotes.Length > 300) return Fail("Notes must be 300 characters or fewer.");

        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            // Match the opening-hours writer's lock order; serialize scheduling,
            // opening-hour changes and other updates for the same staff member.
            _ = await _db.RestaurantProfiles.FromSqlInterpolated(
                $"SELECT * FROM dbo.RestaurantProfiles WITH (UPDLOCK, HOLDLOCK) WHERE Id = {1}")
                .SingleAsync(ct);
            var staff = await _db.Users.FromSqlInterpolated(
                $"SELECT * FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE Id = {staffId}")
                .SingleOrDefaultAsync(ct);
            if (staff is null || !staff.IsActive ||
                (staff.Role != "Waiter" && staff.Role != "Kitchen Staff"))
                return Fail("Only active Waiters and Kitchen Staff can be scheduled.");

            StaffShift? shift = null;
            if (shiftId > 0)
            {
                shift = await _db.StaffShifts.SingleOrDefaultAsync(s => s.Id == shiftId, ct);
                if (shift is null) return Fail("The shift no longer exists.");
            }
            var hours = await _db.OperatingHours.AsNoTracking()
                .Select(h => new WeeklyHours(h.DayOfWeek, h.IsClosed, h.OpeningTime, h.ClosingTime))
                .ToListAsync(ct);
            if (!OpeningHoursPolicy.IsWithinHours(startLocal, endLocal, hours))
                return Fail("The full shift must fit inside restaurant opening hours (overnight hours are supported).");
            var settings = await _db.SystemSettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct);
            var rule = new SystemSettingsInput(settings.CustomerBookingsEnabled,
                settings.MinimumBookingNoticeMinutes, settings.MaximumBookingAdvanceDays,
                settings.MaximumShiftHours, settings.MaximumWeeklyHours, settings.MinimumRestHours);
            var from = startLocal.AddDays(-8);
            var until = endLocal.AddDays(8);
            var otherShifts = await _db.StaffShifts.AsNoTracking()
                .Where(s => s.StaffUserId == staffId && s.Id != shiftId &&
                    s.StartLocal < until && s.EndLocal > from)
                .Select(s => new { s.StartLocal, s.EndLocal }).ToListAsync(ct);
            var problem = StaffSchedulingPolicy.Validate(startLocal, endLocal,
                otherShifts.Select(s => (s.StartLocal, s.EndLocal)), rule);
            if (problem is not null) return Fail(problem);

            if (shift is null)
            {
                shift = new StaffShift { StaffUserId = staffId, CreatedUtc = DateTime.UtcNow };
                _db.StaffShifts.Add(shift);
            }
            else shift.StaffUserId = staffId;
            shift.StartLocal = DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified);
            shift.EndLocal = DateTime.SpecifyKind(endLocal, DateTimeKind.Unspecified);
            shift.Notes = cleanNotes;
            shift.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Ok("Shift saved successfully.");
        }
        catch (DbUpdateException ex)
        {
            _log.LogError(ex, "Could not save staff shift");
            return Fail("Unable to save shift. Please retry.");
        }
        catch (SqlException ex) when (ex.Number is 1205 or 1222)
        {
            _log.LogWarning(ex, "Shift scheduling lock conflict");
            return Fail("Scheduling changed while saving. Please retry.");
        }
    }

    public async Task<OperationResult> MoveAsync(int shiftId, DateOnly newDate, CancellationToken ct = default)
    {
        if (newDate.Year < 2020 || newDate.Year > 2100) return Fail("Invalid target date.");
        // SaveAsync rechecks ownership, opening hours, overlaps and hours limits.
        var old = await _db.StaffShifts.AsNoTracking().SingleOrDefaultAsync(s => s.Id == shiftId, ct);
        if (old is null) return Fail("The shift no longer exists.");
        var start = newDate.ToDateTime(TimeOnly.FromDateTime(old.StartLocal));
        return await SaveAsync(shiftId, old.StaffUserId, start,
            start.Add(old.EndLocal - old.StartLocal), old.Notes, ct);
    }

    public async Task<OperationResult> DeleteAsync(int shiftId, CancellationToken ct = default)
    {
        try
        {
            var shift = await _db.StaffShifts.FindAsync(new object?[] { shiftId }, ct);
            if (shift is null) return Fail("The shift no longer exists.");
            _db.StaffShifts.Remove(shift);
            await _db.SaveChangesAsync(ct);
            return Ok("Shift removed.");
        }
        catch (DbUpdateException ex)
        {
            _log.LogError(ex, "Could not remove shift");
            return Fail("Unable to remove shift.");
        }
    }

    public static DateOnly MondayOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    private static OperationResult Ok(string message) => new(true, message);
    private static OperationResult Fail(string message) => new(false, message);
}
