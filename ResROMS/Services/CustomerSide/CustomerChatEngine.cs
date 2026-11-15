using Microsoft.EntityFrameworkCore;
using ResROMS.Data;

namespace ResROMS.Services.CustomerSide;

/// <summary>
/// Rule-based restaurant assistant: matches keywords and answers from live database data.
/// It is not a language model and makes no external calls.
/// </summary>
public static class CustomerChatEngine
{
    private static bool Has(string text, params string[] words) => words.Any(text.Contains);

    public static async Task<string> ReplyAsync(AppDbContext db, int customerId, string raw, CancellationToken ct = default)
    {
        var text = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (text.Length > 200) text = text[..200];
        if (text.Length == 0)
            return "Hi! Ask me about the menu, reservations, opening hours, payments or your latest order.";

        var items = await db.MenuItems.AsNoTracking().Include(m => m.Category).ToListAsync(ct);

        // 1. A specific dish
        var dish = items.FirstOrDefault(m => text.Contains(m.Name.ToLowerInvariant()));
        if (dish != null)
        {
            var tag = string.IsNullOrWhiteSpace(dish.DietaryTag) ? "" : $" ({dish.DietaryTag})";
            return $"{dish.Name}{tag}: {dish.Description} Price: {CustomerUi.Money(dish.Price)}. " +
                   (dish.IsAvailable ? "It's available now." : "Sorry, it's unavailable at the moment.");
        }

        // 2. Order tracking
        if (Has(text, "my order", "order status", "track", "where is my"))
        {
            var last = await db.Orders.AsNoTracking().Include(o => o.Payment)
                .Where(o => o.CustomerId == customerId).OrderByDescending(o => o.OrderedUtc).FirstOrDefaultAsync(ct);
            return last == null
                ? "You haven't placed an order yet. Use Order Online to place one."
                : $"Your latest order #{last.Id} is: {CustomerUi.OrderLabel(last.Status)} (payment: {last.Payment?.Status ?? "Unpaid"}). Follow it live on My Orders.";
        }

        // 3. Changing / cancelling
        if (Has(text, "cancel", "modify", "change", "reschedule"))
            return "On the Reservations page use Modify or Cancel next to a booking. An order can be cancelled from My Orders while it is still 'Received'.";

        // 4. Reservations
        if (Has(text, "reserv", "book", "table"))
        {
            var setting = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            if (setting is { CustomerBookingsEnabled: false })
                return "Online reservations are temporarily paused. Please contact the restaurant directly.";
            var tables = await db.Tables.AsNoTracking().Where(t => t.Status != "Maintenance").ToListAsync(ct);
            var largest = tables.Count == 0 ? 0 : tables.Max(t => t.Capacity);
            return $"Open the Reservations page, choose a date, time, duration and party size, and pick a free table. We have {tables.Count} tables (largest seats {largest}). Times are Malaysia time (MYT).";
        }

        // 5. Dietary questions
        foreach (var diet in new[] { "vegetarian", "vegan", "halal", "gluten", "spicy" })
        {
            if (!text.Contains(diet)) continue;
            var match = items.Where(m => m.IsAvailable && m.DietaryTag != null
                                         && m.DietaryTag.ToLower().Contains(diet)).ToList();
            return match.Count > 0
                ? $"{char.ToUpper(diet[0]) + diet[1..]} options: " + string.Join(", ", match.Select(m => $"{m.Name} ({CustomerUi.Money(m.Price)})")) + "."
                : $"No dishes are tagged {diet} right now.";
        }

        // 6. Popular / recommendations
        if (Has(text, "popular", "best", "recommend", "suggest", "favourite", "favorite"))
        {
            var set = await CustomerRecommendations.BuildAsync(db, customerId, ct);
            return "Most popular right now: " + string.Join(", ", set.Popular.Select(m => m.Name)) +
                   ". Your dashboard also shows picks based on your orders.";
        }

        // 7. Menu / prices
        if (Has(text, "menu", "price", "cost", "dish", "food", "drink", "eat"))
        {
            var cats = items.Where(m => m.IsAvailable).GroupBy(m => m.Category?.Name ?? "Other")
                .Select(g => $"{g.Key} ({g.Count()})");
            return "Our menu has: " + string.Join(", ", cats) + ". Ask about a dish by name for its price, or open the Menu page.";
        }

        // 8. Opening hours (from the restaurant's real opening-hours settings)
        if (Has(text, "hour", "open", "close", "when"))
        {
            var hours = await db.OperatingHours.AsNoTracking().OrderBy(h => h.DayOfWeek).ToListAsync(ct);
            if (hours.Count == 0) return "Please contact the restaurant for opening hours.";
            string Day(int d) => ((DayOfWeek)d).ToString()[..3];
            return "Opening hours (MYT): " + string.Join("; ", hours.Select(h =>
                h.IsClosed ? $"{Day(h.DayOfWeek)} closed"
                           : $"{Day(h.DayOfWeek)} {h.OpeningTime.ToString("HH:mm")}-{h.ClosingTime.ToString("HH:mm")}")) + ".";
        }

        // 9. Payments
        if (Has(text, "pay", "card", "cash", "refund", "transfer"))
            return "Payments are recorded by our staff (cash, card or bank transfer). You can see each payment under History > Payments.";

        // 10. Notifications / promotions
        if (Has(text, "notif", "alert", "offer", "promo", "discount"))
            return "Reservation, order, payment and promotion updates appear on the Notifications page. Current offers are on your dashboard.";

        if (Has(text, "hello", "hi ", "hey", "thanks", "thank"))
            return "Hello! How can I help? Ask about the menu, reservations, opening hours or your order.";

        return "Sorry, I didn't catch that. Try: the menu, a dish name, vegetarian options, reservations, opening hours, payments or your order status.";
    }
}
