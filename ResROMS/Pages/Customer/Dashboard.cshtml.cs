using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;
using ResROMS.Services;
using ResROMS.Services.CustomerSide;

namespace ResROMS.Pages.Customer;

public sealed class DashboardModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IAnnouncementService _announcements;

    public DashboardModel(AppDbContext db, IAnnouncementService announcements)
    {
        _db = db;
        _announcements = announcements;
    }

    public string FullName { get; private set; } = string.Empty;
    public int UpcomingReservations { get; private set; }
    public int ActiveOrders { get; private set; }
    public int UnreadNotifications { get; private set; }
    public IReadOnlyList<AnnouncementView> Announcements { get; private set; } = [];
    public List<Campaign> Offers { get; private set; } = new();
    public RecommendationSet Recommendations { get; private set; } = new(new(), new(), string.Empty);

    public async Task OnGetAsync()
    {
        var id = User.GetCustomerId();
        FullName = User.Identity?.Name ?? "Guest";
        var now = DateTime.UtcNow;

        UpcomingReservations = await _db.Reservations.CountAsync(r => r.CustomerId == id
            && r.StartUtc > now && (r.Status == "Pending" || r.Status == "Confirmed"));
        ActiveOrders = await _db.Orders.CountAsync(o => o.CustomerId == id
            && (o.Status == "New" || o.Status == "Cooking" || o.Status == "Ready"));

        var seen = CustomerNotificationFeed.ReadSeen(Request, id);
        UnreadNotifications = (await CustomerNotificationFeed.BuildAsync(_db, id)).Count(n => n.WhenUtc > seen);

        Announcements = (await _announcements.GetLiveForRoleAsync("Customer")).Take(3).ToList();

        var today = ReservationClock.NowLocal.Date;
        Offers = await _db.Campaigns.AsNoTracking()
            .Where(c => c.IsEnabled && c.StartDate <= today && c.EndDate >= today)
            .OrderBy(c => c.EndDate).Take(3).ToListAsync();

        Recommendations = await CustomerRecommendations.BuildAsync(_db, id);
    }
}
