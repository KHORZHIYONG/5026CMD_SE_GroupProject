using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;
using ResROMS.Services;
using ResROMS.Services.CustomerSide;

namespace ResROMS.Pages.Customer;

/// <summary>Customer ordering. Orders are created through the existing IOrderManagementService, so all of its validation and pricing rules apply.</summary>
public sealed class OrderModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IOrderManagementService _orders;

    public OrderModel(AppDbContext db, IOrderManagementService orders)
    {
        _db = db;
        _orders = orders;
    }

    // key = menu item id, value = quantity
    [BindProperty] public Dictionary<int, int> Qty { get; set; } = new();
    [BindProperty] public string? PromoCode { get; set; }
    [BindProperty] public string OrderType { get; set; } = "Dine-in";
    [BindProperty] public int? TableId { get; set; }
    [BindProperty] public int? ReservationId { get; set; }

    // The customer's confirmed bookings that have not ended yet (an order can be linked to one)
    public List<Reservation> MyReservations { get; private set; } = new();

    public List<Category> Categories { get; private set; } = new();
    public IReadOnlyList<TableChoice> Tables { get; private set; } = [];

    private async Task LoadAsync()
    {
        var categories = await _db.Categories.AsNoTracking()
            .Include(c => c.MenuItems.Where(m => m.IsAvailable)).OrderBy(c => c.Name).ToListAsync();
        Categories = categories.Where(c => c.MenuItems.Count > 0).ToList();
        Tables = await _orders.GetTableChoicesAsync();

        var id = User.GetCustomerId();
        var now = DateTime.UtcNow;
        MyReservations = await _db.Reservations.AsNoTracking().Include(r => r.Table)
            .Where(r => r.CustomerId == id && r.Status == "Confirmed" && r.EndUtc > now && r.StartUtc < now.AddHours(24))
            .OrderBy(r => r.StartUtc).Take(5).ToListAsync();
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var customerId = User.GetCustomerId();
        if (customerId == 0) return Challenge();

        var lines = Qty.Where(q => q.Value > 0)
            .Select(q => new OrderLineInput(q.Key, Math.Min(q.Value, 99))).ToList();
        if (lines.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Please add at least one item.");
            await LoadAsync();
            return Page();
        }

        int? tableId = null;
        int? reservationId = null;
        if (ReservationId.HasValue)
        {
            // Linked order: it is served at the reserved table. Only the customer's own booking is accepted;
            // the order service re-checks that it is confirmed and that customer and table match.
            var booking = await _db.Reservations.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == ReservationId.Value && r.CustomerId == customerId);
            if (booking == null)
            {
                ModelState.AddModelError(string.Empty, "That reservation could not be found.");
                await LoadAsync();
                return Page();
            }
            reservationId = booking.Id;
            tableId = booking.TableId;
        }
        else if (OrderType == "Dine-in")
        {
            if (!TableId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "Please choose your table for a dine-in order, or switch to Takeaway.");
                await LoadAsync();
                return Page();
            }
            tableId = TableId;
        }

        var result = await _orders.CreateAsync(new CreateOrderInput(customerId, tableId, reservationId, lines, customerId, PromoCode));
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await LoadAsync();
            return Page();
        }

        TempData["SuccessMessage"] = result.Message.Replace(" and stored in SQL Server", "") + " Track it below.";
        return RedirectToPage("/Customer/Orders");
    }
}
