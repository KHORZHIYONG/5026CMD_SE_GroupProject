using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Data;
using ResROMS.Services.CustomerSide;

namespace ResROMS.Pages.Customer;

public sealed class NotificationsModel : PageModel
{
    private readonly AppDbContext _db;
    public NotificationsModel(AppDbContext db) => _db = db;

    public List<CustomerNotification> Items { get; private set; } = new();
    public DateTime LastSeenUtc { get; private set; }

    public async Task OnGetAsync()
    {
        var id = User.GetCustomerId();
        LastSeenUtc = CustomerNotificationFeed.ReadSeen(Request, id);
        Items = await CustomerNotificationFeed.BuildAsync(_db, id);

        // Opening this page marks everything as seen (highlights for this visit are kept)
        CustomerNotificationFeed.MarkAllSeen(Response, id);
    }
}
