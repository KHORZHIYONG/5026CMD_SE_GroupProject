using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages.Staff;

// Kitchen board: a simple screen for the people who cook.
// It shows only the orders the kitchen has to deal with and has two big buttons.
// Like the Orders page, it does not touch the database directly.
// It asks IOrderManagementService (the shared service) to change the status.
public sealed class KitchenModel : PageModel
{
    private readonly IOrderManagementService _orders;
    public KitchenModel(IOrderManagementService orders) => _orders = orders;

    public IReadOnlyList<OrderSummary> NewOrders { get; private set; } = [];
    public IReadOnlyList<OrderSummary> CookingOrders { get; private set; } = [];
    public IReadOnlyList<OrderSummary> ReadyOrders { get; private set; } = [];

    // Local Malaysia time now, used to work out "waiting 12 min"
    public DateTime NowLocal { get; private set; }

    public async Task OnGetAsync()
    {
        var all = await _orders.GetOrdersAsync();
        // Oldest first, so the order that has waited longest is on top
        NewOrders = all.Where(o => o.Status == "New").OrderBy(o => o.OrderedLocal).ToArray();
        CookingOrders = all.Where(o => o.Status == "Cooking").OrderBy(o => o.OrderedLocal).ToArray();
        ReadyOrders = all.Where(o => o.Status == "Ready").OrderBy(o => o.OrderedLocal).ToArray();
        NowLocal = ReservationClock.ToLocal(DateTime.UtcNow);
    }

    // The kitchen can only do two moves: New -> Cooking and Cooking -> Ready.
    // Anything else (like Cancelled or Completed) is refused here on the server,
    // even if someone changes the form in the browser.
    public async Task<IActionResult> OnPostUpdateStatusAsync(int orderId, string newStatus)
    {
        if (newStatus is not ("Cooking" or "Ready"))
        {
            TempData["ErrorMessage"] = "The kitchen can only start cooking or mark an order ready.";
            return RedirectToPage();
        }

        var result = await _orders.UpdateStatusAsync(orderId, newStatus);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }
}