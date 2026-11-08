using Microsoft.EntityFrameworkCore;
using ResROMS.Data;

namespace ResROMS.Services.CustomerSide;

public sealed record CustomerNotification(string Type, string Title, string Message, DateTime WhenUtc);

/// <summary>
/// Builds a customer's notification feed from data that already exists (reservations, orders,
/// payments, campaigns, announcements). Nothing extra is stored: "unread" is tracked by a
/// per-user "last seen" cookie, so no schema change is needed.
/// </summary>
public static class CustomerNotificationFeed
{
    public const int WindowDays = 30;

    public static string SeenCookieName(int customerId) => $"ResROMS.NotifSeen.{customerId}";

    public static DateTime ReadSeen(HttpRequest request, int customerId) =>
        request.Cookies.TryGetValue(SeenCookieName(customerId), out var raw)
        && long.TryParse(raw, out var ticks) && ticks > 0 && ticks < DateTime.MaxValue.Ticks
            ? new DateTime(ticks, DateTimeKind.Utc)
            : DateTime.MinValue;

    public static void MarkAllSeen(HttpResponse response, int customerId) =>
        response.Cookies.Append(SeenCookieName(customerId), DateTime.UtcNow.Ticks.ToString(),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });

    public static async Task<List<CustomerNotification>> BuildAsync(AppDbContext db, int customerId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var since = now.AddDays(-WindowDays);
        var items = new List<CustomerNotification>();

        // Reservations
        var reservations = await db.Reservations.AsNoTracking().Include(r => r.Table)
            .Where(r => r.CustomerId == customerId && r.UpdatedUtc >= since).ToListAsync(ct);
        foreach (var r in reservations)
        {
            var when = $"{CustomerUi.Local(r.StartUtc)} (table {r.Table.TableNumber})";
            var (title, text) = r.Status switch
            {
                "Pending" => ("Reservation received", $"We received your request for {when}. It is awaiting confirmation."),
                "Confirmed" => ("Reservation confirmed", $"Your booking for {when} is confirmed."),
                "Cancelled" => ("Reservation cancelled", $"Your booking for {when} was cancelled."),
                "Completed" => ("Thanks for dining with us", $"Your visit on {when} is complete. Please rate your experience."),
                _ => ("Reservation updated", $"Your booking for {when} was updated.")
            };
            items.Add(new CustomerNotification("Reservation", title, text, r.UpdatedUtc));
        }

        // Orders
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId && o.UpdatedUtc >= since).ToListAsync(ct);
        foreach (var o in orders)
        {
            var text = o.Status switch
            {
                "New" => $"Order #{o.Id} was received.",
                "Cooking" => $"Order #{o.Id} is now being prepared.",
                "Ready" => $"Order #{o.Id} is ready!",
                "Completed" => $"Order #{o.Id} is completed. Enjoy, and please rate it.",
                "Cancelled" => $"Order #{o.Id} was cancelled.",
                _ => $"Order #{o.Id} was updated."
            };
            items.Add(new CustomerNotification("Order", $"Order #{o.Id} update", text, o.UpdatedUtc));
        }

        // Payments
        var payments = await db.Payments.AsNoTracking().Include(p => p.Order)
            .Where(p => p.Order.CustomerId == customerId && p.RecordedUtc >= since).ToListAsync(ct);
        foreach (var p in payments)
        {
            items.Add(new CustomerNotification("Payment", "Payment confirmed",
                $"We recorded your {p.Method} payment of {CustomerUi.Money(p.Amount)} for order #{p.OrderId}.", p.RecordedUtc));
            if (p.Status == "Refunded" && p.RefundedUtc.HasValue)
                items.Add(new CustomerNotification("Payment", "Payment refunded",
                    $"Your payment of {CustomerUi.Money(p.Amount)} for order #{p.OrderId} was marked as refunded.", p.RefundedUtc.Value));
        }

        // Promotions: new offers and announcements aimed at customers
        var today = ReservationClock.NowLocal.Date;
        var campaigns = await db.Campaigns.AsNoTracking()
            .Where(c => c.IsEnabled && c.EndDate >= today && c.StartDate <= today && c.StartDate >= today.AddDays(-14))
            .ToListAsync(ct);
        foreach (var c in campaigns)
        {
            var code = string.IsNullOrWhiteSpace(c.PromoCode) ? "" : $" Code: {c.PromoCode}.";
            items.Add(new CustomerNotification("Promotion", $"Offer: {c.Name}", $"{c.Description}{code}", ReservationClock.ToUtc(c.StartDate)));
        }

        var announcements = await db.Announcements.AsNoTracking()
            .Where(a => a.IsPublished && !a.IsArchived && a.StartsUtc <= now && a.StartsUtc >= since
                        && (!a.EndsUtc.HasValue || a.EndsUtc.Value > now)
                        && (a.Audience == "All" || a.Audience == "Customer"))
            .ToListAsync(ct);
        foreach (var a in announcements)
            items.Add(new CustomerNotification("Promotion", a.Title, a.Body.Length > 200 ? a.Body[..200] + "…" : a.Body, a.StartsUtc));

        return items.OrderByDescending(i => i.WhenUtc).Take(50).ToList();
    }
}
