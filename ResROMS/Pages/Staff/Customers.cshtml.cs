using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Services;

namespace ResROMS.Pages.Staff;

// Read-only list of Customer accounts with their reservation and order history.
// Customers are now real accounts (role "Customer"), so we no longer have to
// guess who is who by phone number like the first version did.
public sealed class CustomersModel : PageModel
{
    private readonly AppDbContext _db;
    public CustomersModel(AppDbContext db) => _db = db;

    // What was typed in the search box (name, email or phone)
    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    // Which customer is open in the right hand panel
    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    public List<CustomerRow> Customers { get; private set; } = new();
    public CustomerRow? Selected { get; private set; }
    public List<ReservationLine> Reservations { get; private set; } = new();
    public List<OrderLine> Orders { get; private set; } = new();

    public sealed class CustomerRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public bool IsActive { get; set; }
        public int TotalReservations { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public DateTime? LastReservationLocal { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public sealed record ReservationLine(DateTime StartLocal, string Table, int PartySize,
        string Status, string Requests);
    public sealed record OrderLine(int Id, DateTime OrderedLocal, string Status,
        decimal Total, string PaymentStatus);

    public async Task OnGetAsync()
    {
        var users = await _db.Users.AsNoTracking()
            .Where(u => u.Role == "Customer")
            .OrderBy(u => u.FullName).ToListAsync();

        var reservations = await _db.Reservations.AsNoTracking()
            .Select(r => new { r.CustomerId, r.Status, r.StartUtc })
            .ToListAsync();

        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.CustomerId != null && o.Status != "Cancelled")
            .Select(o => new { CustomerId = o.CustomerId!.Value, o.TotalAmount, Paid = o.Payment != null && o.Payment.Status == "Paid" })
            .ToListAsync();

        Customers = users.Select(u =>
        {
            var rs = reservations.Where(r => r.CustomerId == u.Id).ToList();
            var os = orders.Where(o => o.CustomerId == u.Id).ToList();
            return new CustomerRow
            {
                Id = u.Id,
                Name = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                IsActive = u.IsActive,
                TotalReservations = rs.Count,
                Completed = rs.Count(r => r.Status == "Completed"),
                Cancelled = rs.Count(r => r.Status == "Cancelled"),
                LastReservationLocal = rs.Count == 0 ? null
                    : ReservationClock.ToLocal(rs.Max(r => r.StartUtc)),
                TotalOrders = os.Count,
                TotalSpent = os.Where(o => o.Paid).Sum(o => o.TotalAmount)
            };
        }).ToList();

        // Search by name, email or phone digits
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim().ToLowerInvariant();
            var digits = new string(term.Where(char.IsDigit).ToArray());
            Customers = Customers.Where(c =>
                c.Name.ToLowerInvariant().Contains(term) ||
                c.Email.ToLowerInvariant().Contains(term) ||
                (digits.Length > 0 && new string(c.Phone.Where(char.IsDigit).ToArray()).Contains(digits))
            ).ToList();
        }

        // Open the chosen customer, or the first in the list
        Selected = Customers.FirstOrDefault(c => c.Id == Id) ?? Customers.FirstOrDefault();
        if (Selected == null) return;

        Reservations = (await _db.Reservations.AsNoTracking()
                .Include(r => r.Table)
                .Where(r => r.CustomerId == Selected.Id)
                .OrderByDescending(r => r.StartUtc).Take(50).ToListAsync())
            .Select(r => new ReservationLine(ReservationClock.ToLocal(r.StartUtc),
                r.Table.TableNumber, r.PartySize, r.Status, r.SpecialRequests))
            .ToList();

        Orders = (await _db.Orders.AsNoTracking()
                .Include(o => o.Payment)
                .Where(o => o.CustomerId == Selected.Id)
                .OrderByDescending(o => o.OrderedUtc).Take(50).ToListAsync())
            .Select(o => new OrderLine(o.Id, ReservationClock.ToLocal(o.OrderedUtc), o.Status,
                o.TotalAmount, o.Payment?.Status ?? "Unpaid"))
            .ToList();
    }
}