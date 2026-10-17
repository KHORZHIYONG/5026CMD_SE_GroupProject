using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;

namespace ResROMS.Services;

// Admin setup rules are kept outside Razor PageModels.
public sealed class RestaurantManagementService : IRestaurantManagementService
{
    private readonly AppDbContext _db;
    private readonly ILogger<RestaurantManagementService> _logger;

    public RestaurantManagementService(AppDbContext db, ILogger<RestaurantManagementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<RestaurantDetails> GetDetailsAsync()
    {
        var profile = await _db.RestaurantProfiles.AsNoTracking().SingleAsync(p => p.Id == 1);
        return new RestaurantDetails(profile.Name, profile.Phone, profile.Email, profile.Address);
    }

    public async Task<IReadOnlyList<WeeklyHours>> GetHoursAsync() =>
        await _db.OperatingHours.AsNoTracking().OrderBy(h => h.DayOfWeek)
            .Select(h => new WeeklyHours(h.DayOfWeek, h.IsClosed, h.OpeningTime, h.ClosingTime))
            .ToListAsync();

    public async Task<RestaurantSaveResult> SaveDetailsAsync(RestaurantDetails details)
    {
        var name = (details.Name ?? string.Empty).Trim();
        var email = (details.Email ?? string.Empty).Trim();
        var phone = (details.Phone ?? string.Empty).Trim();
        var address = (details.Address ?? string.Empty).Trim();
        if (name.Length is < 1 or > 120 || phone.Length > 40 || email.Length > 254 ||
            address.Length is < 1 or > 500)
            return Fail("Check the restaurant name, contact details and address lengths.");
        if (email.Length > 0 && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            return Fail("Enter a valid support email address.");

        try
        {
            var profile = await _db.RestaurantProfiles.SingleAsync(p => p.Id == 1);
            profile.Name = name;
            profile.Phone = phone;
            profile.Email = email;
            profile.Address = address;
            profile.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok("Restaurant information saved to SQL Server.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Saving restaurant profile failed");
            return Fail("Could not save restaurant information. Please retry.");
        }
    }

    public async Task<RestaurantSaveResult> SaveHoursAsync(IReadOnlyList<WeeklyHours> schedule)
    {
        if (schedule.Count != 7 || schedule.Select(s => s.DayOfWeek).Distinct().Count() != 7 ||
            schedule.Any(s => s.DayOfWeek is < 0 or > 6))
            return Fail("Provide exactly one entry for each day from Sunday to Saturday.");
        if (schedule.Any(s => !s.IsClosed && s.OpeningTime == s.ClosingTime))
            return Fail("Opening and closing times cannot be equal on an open day.");

        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            // IMPORTANT: Reservations use the same lock BEFORE validating hours.
            // This prevents opening-hours changes racing against new bookings.
            var profile = await _db.RestaurantProfiles.FromSqlInterpolated(
                $"SELECT * FROM dbo.RestaurantProfiles WITH (UPDLOCK, HOLDLOCK) WHERE Id = {1}")
                .SingleAsync();
            var nowUtc = DateTime.UtcNow;
            var upcoming = await _db.Reservations.AsNoTracking()
                .Where(r => (r.Status == "Pending" || r.Status == "Confirmed") && r.EndUtc > nowUtc)
                .OrderBy(r => r.StartUtc)
                .Select(r => new { r.Id, r.StartUtc, r.EndUtc })
                .ToListAsync();

            foreach (var booking in upcoming)
            {
                var start = ReservationClock.ToLocal(booking.StartUtc);
                var end = ReservationClock.ToLocal(booking.EndUtc);
                if (!OpeningHoursPolicy.IsWithinHours(start, end, schedule))
                    return Fail($"Reservation #{booking.Id} ({start:dd MMM yyyy HH:mm}) would be outside the new opening hours. Move or cancel it first.");
            }

            var existing = await _db.OperatingHours.ToDictionaryAsync(h => h.DayOfWeek);
            foreach (var selected in schedule)
            {
                if (!existing.TryGetValue(selected.DayOfWeek, out var row))
                {
                    row = new OperatingHour { DayOfWeek = selected.DayOfWeek };
                    _db.OperatingHours.Add(row);
                }
                row.IsClosed = selected.IsClosed;
                row.OpeningTime = selected.OpeningTime;
                row.ClosingTime = selected.ClosingTime;
            }
            profile.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok("Operating hours saved. Future reservations will follow this schedule.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Saving opening hours failed");
            return Fail("Could not save operating hours. Please retry.");
        }
        catch (SqlException ex) when (ex.Number is 1205 or 1222)
        {
            _logger.LogWarning(ex, "Opening hours update conflicted with a reservation");
            return Fail("Reservation activity changed during the update. Please retry.");
        }
    }

    private static RestaurantSaveResult Ok(string message) => new(true, message);
    private static RestaurantSaveResult Fail(string message) => new(false, message);
}
