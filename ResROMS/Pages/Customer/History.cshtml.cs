using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;
using ResROMS.Services;
using ResROMS.Services.CustomerSide;

namespace ResROMS.Pages.Customer;

public sealed class HistoryModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IReservationService _reservations;

    public HistoryModel(AppDbContext db, IReservationService reservations)
    {
        _db = db;
        _reservations = reservations;
    }

    public IReadOnlyList<ReservationSummary> Reservations { get; private set; } = [];
    public List<Order> Orders { get; private set; } = new();
    public List<Payment> Payments { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var id = User.GetCustomerId();

        Reservations = await _reservations.GetForCustomerAsync(id);

        Orders = await _db.Orders.AsNoTracking()
            .Include(o => o.Items).Include(o => o.Payment)
            .Where(o => o.CustomerId == id)
            .OrderByDescending(o => o.OrderedUtc).ThenByDescending(o => o.Id).Take(200)
            .AsSplitQuery().ToListAsync();

        Payments = await _db.Payments.AsNoTracking()
            .Where(p => p.Order.CustomerId == id)
            .OrderByDescending(p => p.RecordedUtc).ThenByDescending(p => p.Id).Take(200).ToListAsync();
    }
}
