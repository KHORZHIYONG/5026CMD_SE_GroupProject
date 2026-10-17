using Microsoft.EntityFrameworkCore;
using ResROMS.Data;

namespace ResROMS.Services;

// Admin-only read models. Queries are bounded by a validated date range and
// never mutate orders, refunds, reservations or existing schema.
public sealed class AdminReportingService : IAdminReportingService
{
    private readonly AppDbContext _db;
    public AdminReportingService(AppDbContext db) => _db = db;

    public async Task<AdminDashboard> GetDashboardAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(ReservationClock.NowLocal);
        var start = today.AddDays(-6);
        var weekly = await GetReportAsync(new ReportRange(start, today, "Daily"), ct);
        var day = weekly.Trend.Single(row => row.Label == today.ToString("yyyy-MM-dd"));
        var now = DateTime.UtcNow;

        var activeOrders = await _db.Orders.AsNoTracking()
            .CountAsync(o => o.Status == "New" || o.Status == "Cooking" || o.Status == "Ready", ct);
        // Keep the SQL projection simple. Construct C# record objects only after
        // EF Core has finished executing the grouped query.
        var statusRows = await _db.Tables.AsNoTracking()
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .OrderBy(x => x.Status)
            .ToListAsync(ct);
        var statuses = statusRows
            .Select(x => new StatusBreakdown(x.Status, x.Count))
            .ToArray();
        var upcoming = await _db.Reservations.AsNoTracking()
            .Where(r => r.StartUtc >= now && r.StartUtc < now.AddDays(7)
                && (r.Status == "Pending" || r.Status == "Confirmed"))
            .OrderBy(r => r.StartUtc).Take(8)
            .Select(r => new
            {
                r.Id, r.StartUtc, r.PartySize, r.Status,
                TableNumber = r.Table.TableNumber,
                CustomerName = r.Customer.FullName
            }).ToListAsync(ct);

        return new AdminDashboard(
            day.Reservations,
            activeOrders,
            day.NetCollected,
            weekly.Trend,
            statuses,
            weekly.PopularItems.Take(5).ToArray(),
            upcoming.Select(r => new UpcomingBooking(r.Id, ReservationClock.ToLocal(r.StartUtc),
                r.TableNumber, r.CustomerName, r.PartySize, r.Status)).ToArray());
    }

    public async Task<AdminReport> GetReportAsync(ReportRange range, CancellationToken ct = default)
    {
        // ReportRange is supplied by a PageModel but defensively validate it here too.
        if (range.To < range.From || range.To.DayNumber - range.From.DayNumber > 365
            || range.From.Year < 2000 || range.To.Year > 2100
            || range.GroupBy is not ("Daily" or "Weekly" or "Monthly"))
            throw new ArgumentOutOfRangeException(nameof(range), "Invalid reporting period.");

        var fromUtc = ReservationClock.ToUtc(range.From.ToDateTime(TimeOnly.MinValue));
        var untilUtc = ReservationClock.ToUtc(range.To.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var reservations = await _db.Reservations.AsNoTracking()
            .Where(r => r.StartUtc >= fromUtc && r.StartUtc < untilUtc)
            .Select(r => new { r.StartUtc, r.Status, r.CustomerId })
            .ToListAsync(ct);
        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.OrderedUtc >= fromUtc && o.OrderedUtc < untilUtc)
            .Select(o => new { o.OrderedUtc, o.Status, o.TotalAmount, o.CustomerId })
            .ToListAsync(ct);
        // A refunded payment has BOTH a collection event (RecordedUtc) and a
        // reversal event (RefundedUtc). Reporting each event in its own date
        // avoids rewriting historical revenue when a later refund occurs.
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => (p.RecordedUtc >= fromUtc && p.RecordedUtc < untilUtc)
                || (p.RefundedUtc >= fromUtc && p.RefundedUtc < untilUtc))
            .Select(p => new { p.Amount, p.RecordedUtc, p.RefundedUtc })
            .ToListAsync(ct);

        // Group, aggregate, sort and limit on SQL Server. Materialize the SQL
        // result first, then create MenuPopularity records in .NET. Ordering by
        // a record constructor's properties isn't translatable by all EF providers.
        var popularRows = await _db.OrderItems.AsNoTracking()
            .Where(i => i.Order.OrderedUtc >= fromUtc && i.Order.OrderedUtc < untilUtc
                && i.Order.Status != "Cancelled")
            .GroupBy(i => i.MenuItemId)
            .Select(g => new
            {
                MenuItemId = g.Key,
                Name = g.Max(i => i.ItemName),
                Quantity = g.Sum(i => i.Quantity),
                OrderValue = g.Sum(i => i.LineTotal)
            })
            .OrderByDescending(r => r.Quantity)
            .ThenBy(r => r.Name)
            .Take(10)
            .ToListAsync(ct);
        var popular = popularRows
            .Select(x => new MenuPopularity(x.MenuItemId, x.Name, x.Quantity, x.OrderValue))
            .ToArray();

        var signups = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Role == "Customer"
                && u.DateJoined >= fromUtc && u.DateJoined < untilUtc, ct);

        var activeCustomers = reservations.Select(r => (int?)r.CustomerId)
            .Concat(orders.Select(o => o.CustomerId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value).Distinct().Count();

        var trend = new SortedDictionary<DateOnly, (decimal Net, int Reservations, int Orders)>();
        for (var day = range.From; day <= range.To; day = day.AddDays(1))
        {
            var bucket = Bucket(day, range.GroupBy);
            trend.TryAdd(bucket, (0m, 0, 0));
        }
        foreach (var r in reservations.Where(r => r.Status != "Cancelled"))
        {
            var key = Bucket(DateOnly.FromDateTime(ReservationClock.ToLocal(r.StartUtc)), range.GroupBy);
            var item = trend[key];
            trend[key] = (item.Net, item.Reservations + 1, item.Orders);
        }
        foreach (var o in orders.Where(o => o.Status != "Cancelled"))
        {
            var key = Bucket(DateOnly.FromDateTime(ReservationClock.ToLocal(o.OrderedUtc)), range.GroupBy);
            var item = trend[key];
            trend[key] = (item.Net, item.Reservations, item.Orders + 1);
        }
        decimal collected = 0m, refunded = 0m;
        foreach (var p in payments)
        {
            if (p.RecordedUtc >= fromUtc && p.RecordedUtc < untilUtc)
            {
                collected += p.Amount;
                var key = Bucket(DateOnly.FromDateTime(ReservationClock.ToLocal(p.RecordedUtc)), range.GroupBy);
                var item = trend[key];
                trend[key] = (item.Net + p.Amount, item.Reservations, item.Orders);
            }
            if (p.RefundedUtc is DateTime refundedUtc && refundedUtc >= fromUtc && refundedUtc < untilUtc)
            {
                refunded += p.Amount;
                var key = Bucket(DateOnly.FromDateTime(ReservationClock.ToLocal(refundedUtc)), range.GroupBy);
                var item = trend[key];
                trend[key] = (item.Net - p.Amount, item.Reservations, item.Orders);
            }
        }
        var reservationStatuses = reservations.GroupBy(r => r.Status)
            .OrderBy(g => g.Key)
            .Select(g => new StatusBreakdown(g.Key, g.Count())).ToArray();

        return new AdminReport(
            range,
            reservations.Count(r => r.Status != "Cancelled"),
            reservations.Count(r => r.Status == "Pending"),
            reservations.Count(r => r.Status == "Cancelled"),
            orders.Count(o => o.Status != "Cancelled"),
            orders.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount),
            collected,
            refunded,
            collected - refunded,
            signups,
            activeCustomers,
            trend.Select(kv => new ReportTrend(FormatBucket(kv.Key, range.GroupBy),
                kv.Value.Net, kv.Value.Reservations, kv.Value.Orders)).ToArray(),
            popular,
            reservationStatuses);
    }

    private static DateOnly Bucket(DateOnly day, string grouping) => grouping switch
    {
        "Monthly" => new DateOnly(day.Year, day.Month, 1),
        "Weekly" => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)), // Monday-start
        _ => day
    };

    private static string FormatBucket(DateOnly key, string grouping) => grouping switch
    {
        "Monthly" => key.ToString("yyyy-MM"),
        _ => key.ToString("yyyy-MM-dd") // Weekly label is the Monday starting date
    };
}
