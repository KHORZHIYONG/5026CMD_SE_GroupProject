using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;
using ResROMS.Services.CustomerSide;

namespace ResROMS.Pages.Customer;

public sealed class FeedbackModel : PageModel
{
    private readonly AppDbContext _db;
    public FeedbackModel(AppDbContext db) => _db = db;

    public List<Order> PendingOrders { get; private set; } = new();
    public List<Reservation> PendingReservations { get; private set; } = new();
    public List<Feedback> Past { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var id = User.GetCustomerId();

        var ratedOrders = await _db.Feedbacks.AsNoTracking()
            .Where(f => f.CustomerId == id && f.OrderId != null).Select(f => f.OrderId!.Value).ToListAsync();
        var ratedReservations = await _db.Feedbacks.AsNoTracking()
            .Where(f => f.CustomerId == id && f.ReservationId != null).Select(f => f.ReservationId!.Value).ToListAsync();

        PendingOrders = await _db.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => o.CustomerId == id && o.Status == "Completed" && !ratedOrders.Contains(o.Id))
            .OrderByDescending(o => o.OrderedUtc).Take(10).ToListAsync();

        PendingReservations = await _db.Reservations.AsNoTracking().Include(r => r.Table)
            .Where(r => r.CustomerId == id && r.Status == "Completed" && !ratedReservations.Contains(r.Id))
            .OrderByDescending(r => r.StartUtc).Take(10).ToListAsync();

        Past = await _db.Feedbacks.AsNoTracking()
            .Where(f => f.CustomerId == id).OrderByDescending(f => f.CreatedUtc).Take(20).ToListAsync();
    }

    public async Task<IActionResult> OnPostSubmitAsync(string kind, int refId, int rating, string? comment)
    {
        var id = User.GetCustomerId();
        if (rating < 1 || rating > 5) return RedirectToPage();
        comment = (comment ?? string.Empty).Trim();
        if (comment.Length > 500) comment = comment[..500];

        var feedback = new Feedback { CustomerId = id, Rating = rating, Comment = comment };
        bool allowed;
        if (kind == "order")
        {
            // Customers may only rate their own completed orders, once each
            allowed = await _db.Orders.AnyAsync(o => o.Id == refId && o.CustomerId == id && o.Status == "Completed")
                      && !await _db.Feedbacks.AnyAsync(f => f.OrderId == refId);
            feedback.OrderId = refId;
        }
        else
        {
            allowed = await _db.Reservations.AnyAsync(r => r.Id == refId && r.CustomerId == id && r.Status == "Completed")
                      && !await _db.Feedbacks.AnyAsync(f => f.ReservationId == refId);
            feedback.ReservationId = refId;
        }

        var saved = false;
        if (allowed)
        {
            try
            {
                _db.Feedbacks.Add(feedback);
                await _db.SaveChangesAsync();
                saved = true;
            }
            catch (DbUpdateException)
            {
                // The unique index rejected a duplicate submitted at the same moment
            }
        }

        TempData[saved ? "SuccessMessage" : "ErrorMessage"] = saved
            ? "Thank you for your feedback!"
            : "That item could not be rated (it may already have feedback).";
        return RedirectToPage();
    }
}
