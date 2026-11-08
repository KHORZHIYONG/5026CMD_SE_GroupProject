using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;
using ResROMS.Services;
using ResROMS.Services.CustomerSide;

namespace ResROMS.Pages.Customer;

public sealed class OrdersModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IOrderManagementService _orders;

    public OrdersModel(AppDbContext db, IOrderManagementService orders)
    {
        _db = db;
        _orders = orders;
    }

    public List<Order> Orders { get; private set; } = new();
    public List<Reservation> Upcoming { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var id = User.GetCustomerId();
        var now = DateTime.UtcNow;

        Orders = await _db.Orders.AsNoTracking()
            .Include(o => o.Items).Include(o => o.Table).Include(o => o.Payment)
            .Where(o => o.CustomerId == id)
            .OrderByDescending(o => o.OrderedUtc).ThenByDescending(o => o.Id).Take(15)
            .AsSplitQuery().ToListAsync();

        Upcoming = await _db.Reservations.AsNoTracking().Include(r => r.Table)
            .Where(r => r.CustomerId == id && r.StartUtc > now && (r.Status == "Pending" || r.Status == "Confirmed"))
            .OrderBy(r => r.StartUtc).Take(5).ToListAsync();
    }

    // Customer preference is NOT proof of payment; staff must verify money was received.
    public async Task<IActionResult> OnPostPaymentPreferenceAsync(int orderId, string method, string? reference)
    {
        int customerId = User.GetCustomerId();
        if (customerId <= 0) return Challenge();
        if (method is not ("Cash" or "Card" or "BankTransfer"))
        {
            TempData["ErrorMessage"] = "Choose a supported payment method.";
            return RedirectToPage();
        }
        reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (reference is { Length: > 100 } || (method == "BankTransfer" && string.IsNullOrWhiteSpace(reference)))
        {
            TempData["ErrorMessage"] = "Bank transfer requests need a reference (up to 100 characters).";
            return RedirectToPage();
        }
        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var order = await _db.Orders.FromSqlInterpolated(
            $"SELECT * FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK) WHERE Id = {orderId}")
            .SingleOrDefaultAsync();
        if (order is null || order.CustomerId != customerId || order.Status == "Cancelled" ||
            await _db.Payments.AnyAsync(p => p.OrderId == orderId))
        {
            TempData["ErrorMessage"] = "This order is not available for a payment request.";
            return RedirectToPage();
        }
        order.PaymentPreference = method;
        order.PaymentReference = method == "BankTransfer" ? reference : null;
        order.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        TempData["SuccessMessage"] = "Payment preference saved. Staff must verify receipt before marking the order paid.";
        return RedirectToPage();
    }

    // A customer may cancel their own order only before it is Ready. The existing service
    // refuses to cancel an order that already has a recorded payment.
    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        var customerId = User.GetCustomerId();
        var owned = await _db.Orders.AsNoTracking().AnyAsync(o => o.Id == id && o.CustomerId == customerId && o.Status == "New");
        if (!owned)
        {
            TempData["ErrorMessage"] = "Only your own orders that have not started cooking can be cancelled. Please ask staff for help.";
            return RedirectToPage();
        }
        var result = await _orders.UpdateStatusAsync(id, "Cancelled");
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }

    // Polled every few seconds by the page so statuses update without a reload
    public async Task<IActionResult> OnGetStatusAsync()
    {
        var id = User.GetCustomerId();
        var now = DateTime.UtcNow;
        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == id && o.OrderedUtc >= now.AddDays(-7))
            .Select(o => new { o.Id, o.Status, Payment = o.Payment != null ? o.Payment.Status : "Unpaid" }).ToListAsync();
        var reservations = await _db.Reservations.AsNoTracking()
            .Where(r => r.CustomerId == id && r.StartUtc > now.AddDays(-1))
            .Select(r => new { r.Id, r.Status }).ToListAsync();
        return new JsonResult(new { orders, reservations });
    }
}
