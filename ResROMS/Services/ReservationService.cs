using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;

namespace ResROMS.Services;

// Business rules live here, not in the Razor page handlers. The per-table SQL
// update lock inside a transaction serializes competing reservations for a table.
public sealed class ReservationService : IReservationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReservationService> _logger;

    public ReservationService(AppDbContext db, ILogger<ReservationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ReservationCustomerOption>> GetCustomersAsync() =>
        await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Role == "Customer" && u.PasswordHash != null)
            .OrderBy(u => u.FullName)
            .Select(u => new ReservationCustomerOption(u.Id, u.FullName, u.Email))
            .ToListAsync();

    public async Task<IReadOnlyList<ReservationTableOption>> GetTablesAsync() =>
        await _db.Tables.AsNoTracking()
            .OrderBy(t => t.TableNumber)
            .Select(t => new ReservationTableOption(t.Id, t.TableNumber, t.Capacity,
                t.DiningArea!.Name, t.Status))
            .ToListAsync();

    public async Task<TableAvailabilityResult> CheckAvailabilityAsync(
        DateTime startLocal, int durationMinutes, int partySize, bool forCustomer = false)
    {
        var problem = await ValidateSlotAsync(startLocal, durationMinutes, partySize, forCustomer);
        if (problem is not null) return new TableAvailabilityResult(problem, []);
        var startUtc = ReservationClock.ToUtc(startLocal);
        var hours = await LoadHoursAsync();
        if (!OpeningHoursPolicy.IsWithinHours(startLocal,
            ReservationClock.ToLocal(startUtc.AddMinutes(durationMinutes)), hours))
            return new TableAvailabilityResult(
                "Your booking would extend beyond opening hours. Choose an earlier start time or shorter duration.", []);
        var tables = await FindAvailableTablesAsync(startLocal, durationMinutes, partySize, forCustomer);
        return new TableAvailabilityResult(null, tables);
    }

    public async Task<IReadOnlyList<ReservationTableOption>> FindAvailableTablesAsync(
        DateTime startLocal, int durationMinutes, int partySize, bool forCustomer = false)
    {
        if (await ValidateSlotAsync(startLocal, durationMinutes, partySize, forCustomer) != null) return [];
        var startUtc = ReservationClock.ToUtc(startLocal);
        var endUtc = startUtc.AddMinutes(durationMinutes);
        var hours = await LoadHoursAsync();
        if (!OpeningHoursPolicy.IsWithinHours(startLocal, ReservationClock.ToLocal(endUtc), hours))
            return [];
        return await _db.Tables.AsNoTracking()
            .Where(t => t.Status != "Maintenance" && t.Capacity >= partySize)
            .Where(t => !_db.Reservations.Any(r => r.TableId == t.Id &&
                (r.Status == "Pending" || r.Status == "Confirmed") &&
                r.StartUtc < endUtc && startUtc < r.EndUtc))
            .OrderBy(t => t.Capacity).ThenBy(t => t.TableNumber)
            .Select(t => new ReservationTableOption(t.Id, t.TableNumber, t.Capacity,
                t.DiningArea!.Name, t.Status))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ReservationSummary>> GetForAdminAsync(DateTime? dateLocal)
    {
        DateTime fromUtc;
        DateTime toUtc;
        if (dateLocal.HasValue)
        {
            fromUtc = ReservationClock.ToUtc(dateLocal.Value.Date);
            toUtc = ReservationClock.ToUtc(dateLocal.Value.Date.AddDays(1));
        }
        else
        {
            fromUtc = DateTime.UtcNow.AddDays(-7);
            toUtc = DateTime.UtcNow.AddDays(90);
        }

        var rows = await _db.Reservations.AsNoTracking()
            .Include(r => r.Customer).Include(r => r.Table)
            .Where(r => r.StartUtc >= fromUtc && r.StartUtc < toUtc)
            .OrderBy(r => r.StartUtc).Take(300).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ReservationSummary>> GetForCustomerAsync(int customerId)
    {
        var rows = await _db.Reservations.AsNoTracking()
            .Include(r => r.Customer).Include(r => r.Table)
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.StartUtc).Take(200).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<ReservationSummary?> FindAsync(int id, int? customerId = null)
    {
        var query = _db.Reservations.AsNoTracking()
            .Include(r => r.Customer).Include(r => r.Table)
            .Where(r => r.Id == id);
        if (customerId.HasValue) query = query.Where(r => r.CustomerId == customerId.Value);
        var row = await query.SingleOrDefaultAsync();
        return row is null ? null : Map(row);
    }

    public Task<ReservationResult> CreateForAdminAsync(int customerId, ReservationRequest input) =>
        SaveBookingAsync(null, customerId, input, isAdmin: true);

    public Task<ReservationResult> CreateForCustomerAsync(int customerId, ReservationRequest input) =>
        SaveBookingAsync(null, customerId, input, isAdmin: false);

    public Task<ReservationResult> UpdateAsync(int reservationId, int? actingCustomerId,
        ReservationRequest input) => SaveBookingAsync(reservationId, actingCustomerId, input,
            isAdmin: !actingCustomerId.HasValue);

    private async Task<ReservationResult> SaveBookingAsync(int? id, int? customerId,
        ReservationRequest input, bool isAdmin)
    {
        var error = await ValidateSlotAsync(input.StartLocal, input.DurationMinutes, input.PartySize,
            forCustomer: !isAdmin);
        if (error != null) return Fail(error);
        var notes = (input.SpecialRequests ?? string.Empty).Trim();
        if (notes.Length > 500) return Fail("Special requests must contain no more than 500 characters.");

        var startUtc = ReservationClock.ToUtc(input.StartLocal);
        var endUtc = startUtc.AddMinutes(input.DurationMinutes);
        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            // Serialize booking creation against changes to restaurant hours.
            var profile = await _db.RestaurantProfiles.FromSqlInterpolated(
                $"SELECT * FROM dbo.RestaurantProfiles WITH (UPDLOCK, HOLDLOCK) WHERE Id = {1}")
                .AsNoTracking().SingleOrDefaultAsync();
            if (profile is null) return Fail("Restaurant settings have not been configured.");
            var hours = await LoadHoursAsync();
            if (!OpeningHoursPolicy.IsWithinHours(input.StartLocal, ReservationClock.ToLocal(endUtc), hours))
                return Fail("The selected reservation must fit completely within the restaurant's opening hours.");

            Reservation? booking = null;
            if (id.HasValue)
            {
                // Lock the current booking while editing; clients cannot change the owner.
                booking = await _db.Reservations.FromSqlInterpolated(
                    $"SELECT * FROM dbo.Reservations WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id.Value}")
                    .SingleOrDefaultAsync();
                if (booking is null) return Fail("Reservation not found.");
                if (customerId.HasValue && booking.CustomerId != customerId.Value)
                    return Fail("Reservation not found.");
                if (booking.Status is not ("Pending" or "Confirmed"))
                    return Fail("Only pending or confirmed reservations may be modified.");
                if (booking.StartUtc <= DateTime.UtcNow)
                    return Fail("A reservation that has already started cannot be modified.");
            }

            int ownerId = booking?.CustomerId ?? customerId ?? 0;
            var customerValid = await _db.Users.AnyAsync(u => u.Id == ownerId &&
                u.Role == "Customer" && u.IsActive && u.PasswordHash != null);
            if (!customerValid) return Fail("Select an active Customer account with login credentials.");

            // UPDLOCK + HOLDLOCK is intentional: overlapping concurrent posts for the
            // same table must wait rather than both reading an empty time slot.
            var table = await _db.Tables.FromSqlInterpolated(
                $"SELECT * FROM dbo.Tables WITH (UPDLOCK, HOLDLOCK) WHERE Id = {input.TableId}")
                .SingleOrDefaultAsync();
            if (table is null) return Fail("Select an existing table.");
            if (table.Status == "Maintenance") return Fail("This table is under maintenance.");
            if (table.Capacity < input.PartySize)
                return Fail($"Table {table.TableNumber} seats {table.Capacity}; choose a larger table.");

            var conflict = await _db.Reservations.AnyAsync(r => r.TableId == table.Id &&
                (!id.HasValue || r.Id != id.Value) &&
                (r.Status == "Pending" || r.Status == "Confirmed") &&
                r.StartUtc < endUtc && startUtc < r.EndUtc);
            if (conflict) return Fail("That table is already reserved during the selected time. Choose another slot or table.");

            if (booking is null)
            {
                booking = new Reservation
                {
                    CustomerId = ownerId, Status = isAdmin ? "Confirmed" : "Pending",
                    CreatedUtc = DateTime.UtcNow
                };
                _db.Reservations.Add(booking);
            }
            else if (!isAdmin)
            {
                // Customer changes must be reviewed again by an Administrator.
                booking.Status = "Pending";
            }

            booking.TableId = table.Id;
            booking.PartySize = input.PartySize;
            booking.StartUtc = startUtc;
            booking.EndUtc = endUtc;
            booking.SpecialRequests = notes;
            booking.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(id.HasValue ? "Reservation updated successfully." :
                isAdmin ? $"Reservation #{booking.Id} confirmed." :
                    $"Reservation #{booking.Id} submitted for confirmation.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Failed to save reservation");
            return Fail("Could not save the reservation. Please retry.");
        }
        catch (SqlException ex) when (ex.Number is 1205 or 1222 or 2601 or 2627)
        {
            _logger.LogWarning(ex, "Reservation write conflict");
            return Fail("Another booking may have changed this slot. Please retry after checking availability.");
        }
    }

    public Task<ReservationResult> ConfirmAsync(int id) => ChangeStatusAsync(id, "Confirmed", null);
    public Task<ReservationResult> CompleteAsync(int id) => ChangeStatusAsync(id, "Completed", null);
    public Task<ReservationResult> CancelAsync(int id, int? actingCustomerId) =>
        ChangeStatusAsync(id, "Cancelled", actingCustomerId);

    private async Task<ReservationResult> ChangeStatusAsync(int id, string target, int? actingCustomerId)
    {
        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var booking = await _db.Reservations.FromSqlInterpolated(
                $"SELECT * FROM dbo.Reservations WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}")
                .SingleOrDefaultAsync();
            if (booking is null || (actingCustomerId.HasValue && booking.CustomerId != actingCustomerId.Value))
                return Fail("Reservation not found.");

            var allowed = target switch
            {
                "Confirmed" => !actingCustomerId.HasValue && booking.Status == "Pending"
                    && booking.StartUtc > DateTime.UtcNow,
                "Completed" => !actingCustomerId.HasValue && booking.Status == "Confirmed"
                    && booking.StartUtc <= DateTime.UtcNow,
                "Cancelled" => (booking.Status is "Pending" or "Confirmed")
                    && (!actingCustomerId.HasValue || booking.StartUtc > DateTime.UtcNow),
                _ => false
            };
            if (!allowed) return Fail("That status change is not allowed for this reservation.");

            booking.Status = target;
            booking.UpdatedUtc = DateTime.UtcNow;
            if (target == "Cancelled") booking.CancelledUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok($"Reservation #{id} marked {target.ToLowerInvariant()}.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Failed to update reservation status");
            return Fail("Could not update the reservation. Please retry.");
        }
        catch (SqlException ex) when (ex.Number is 1205 or 1222)
        {
            _logger.LogWarning(ex, "Reservation status update conflict");
            return Fail("The reservation changed while processing. Please retry.");
        }
    }

    private async Task<IReadOnlyList<WeeklyHours>> LoadHoursAsync() =>
        await _db.OperatingHours.AsNoTracking()
            .Select(h => new WeeklyHours(h.DayOfWeek, h.IsClosed, h.OpeningTime, h.ClosingTime))
            .ToListAsync();

    private async Task<string?> ValidateSlotAsync(DateTime startLocal, int durationMinutes,
        int partySize, bool forCustomer)
    {
        if (partySize is < 1 or > 20) return "Party size must be between 1 and 20.";
        if (durationMinutes is < 30 or > 240 || durationMinutes % 30 != 0)
            return "Duration must be 30–240 minutes in 30-minute steps.";
        if (startLocal == default || startLocal.Second != 0 || startLocal.Minute % 15 != 0)
            return "Choose a start time on a 15-minute interval (e.g. 18:00 or 18:15).";

        var settings = await _db.SystemSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1);
        if (settings is null) return "Reservation policy is not configured. Contact an Administrator.";
        if (forCustomer && !settings.CustomerBookingsEnabled)
            return "Online customer booking requests are temporarily disabled.";
        var now = ReservationClock.NowLocal;
        if (startLocal < now.AddMinutes(settings.MinimumBookingNoticeMinutes))
            return $"Bookings must start at least {settings.MinimumBookingNoticeMinutes} minutes from now.";
        if (startLocal > now.AddDays(settings.MaximumBookingAdvanceDays))
            return $"Bookings may only be made up to {settings.MaximumBookingAdvanceDays} days ahead.";
        return null;
    }

    private static ReservationSummary Map(Reservation r) => new(r.Id, r.CustomerId,
        r.Customer.FullName, r.Customer.Email, r.TableId, r.Table.TableNumber,
        r.PartySize, ReservationClock.ToLocal(r.StartUtc), ReservationClock.ToLocal(r.EndUtc),
        r.Status, r.SpecialRequests);

    private static ReservationResult Ok(string message) => new(true, message);
    private static ReservationResult Fail(string message) => new(false, message);
}
