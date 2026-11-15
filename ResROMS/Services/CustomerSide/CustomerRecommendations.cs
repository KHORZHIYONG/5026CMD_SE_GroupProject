using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;

namespace ResROMS.Services.CustomerSide;

public sealed record RecommendationSet(List<MenuItem> Popular, List<MenuItem> ForYou, string Reason);

public static class CustomerRecommendations
{
    public static async Task<RecommendationSet> BuildAsync(AppDbContext db, int customerId, CancellationToken ct = default)
    {
        var available = await db.MenuItems.AsNoTracking().Include(m => m.Category)
            .Where(m => m.IsAvailable).ToListAsync(ct);

        // Popularity = quantity ordered by all customers (cancelled orders excluded)
        var sales = await db.OrderItems.AsNoTracking()
            .Where(i => i.Order.Status != "Cancelled")
            .GroupBy(i => i.MenuItemId)
            .Select(g => new { Id = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);
        var popularity = sales.ToDictionary(x => x.Id, x => x.Qty);
        int Pop(MenuItem m) => popularity.TryGetValue(m.Id, out var q) ? q : 0;

        var popular = available.OrderByDescending(Pop).ThenBy(m => m.Name).Take(4).ToList();

        // Personalised: dishes from the categories this customer orders from most, not tried yet
        var mine = await db.OrderItems.AsNoTracking()
            .Where(i => i.Order.CustomerId == customerId && i.Order.Status != "Cancelled")
            .Select(i => new { i.MenuItemId, i.MenuItem.CategoryId, i.Quantity })
            .ToListAsync(ct);

        var forYou = new List<MenuItem>();
        var reason = "Popular with our customers";
        if (mine.Count > 0)
        {
            var favourites = mine.GroupBy(x => x.CategoryId)
                .OrderByDescending(g => g.Sum(x => x.Quantity)).Select(g => g.Key).Take(2).ToList();
            var tried = mine.Select(x => x.MenuItemId).ToHashSet();
            forYou = available.Where(m => favourites.Contains(m.CategoryId) && !tried.Contains(m.Id))
                .OrderByDescending(Pop).ThenBy(m => m.Name).Take(4).ToList();
            if (forYou.Count > 0) reason = "Based on what you've ordered before";
        }
        if (forYou.Count == 0) forYou = popular;

        return new RecommendationSet(popular, forYou, reason);
    }
}
