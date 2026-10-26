using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages.Staff;

// Staff Orders page. It does NOT talk to the database directly.
// It asks IOrderManagementService (written by the Admin module) to do the work,
// so Admin and Staff always follow the same rules and see the same orders.
public sealed class OrdersModel : PageModel
{
    private readonly IOrderManagementService _orders;
    public OrdersModel(IOrderManagementService orders) => _orders = orders;

    public IReadOnlyList<OrderSummary> ActiveOrders { get; private set; } = [];
    public IReadOnlyList<MenuChoice> MenuChoices { get; private set; } = [];
    public IReadOnlyList<AccountChoice> CustomerChoices { get; private set; } = [];
    public IReadOnlyList<TableChoice> TableChoices { get; private set; } = [];

    // Form fields posted from the "New Order" popup
    public sealed class LineInput
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public sealed class NewOrderInput
    {
        public int? TableId { get; set; }
        public int? CustomerId { get; set; }
        public List<LineInput> Lines { get; set; } = new();
    }

    [BindProperty] public NewOrderInput NewOrder { get; set; } = new();

    // Kitchen Staff go to their own simple board. They can still open this full page
    // with the "All orders" button, which adds ?all=true.
    public async Task<IActionResult> OnGetAsync(bool all = false)
    {
        if (!all && User.IsInRole("Kitchen Staff")) return RedirectToPage("/Staff/Kitchen");

        var allOrders = await _orders.GetOrdersAsync();
        // Only orders that are still in progress belong on the board.
        ActiveOrders = allOrders.Where(o => o.Status is "New" or "Cooking" or "Ready").ToArray();
        MenuChoices = await _orders.GetMenuChoicesAsync();
        CustomerChoices = await _orders.GetCustomerChoicesAsync();
        TableChoices = await _orders.GetTableChoicesAsync();
        return Page();
    }




    // Called by the notification bell every 10 seconds (see _StaffLayout.cshtml).
    // It only counts orders and returns two numbers as JSON. Nothing is changed.
    public async Task<IActionResult> OnGetCountsAsync()
    {
        var all = await _orders.GetOrdersAsync();
        return new JsonResult(new
        {
            ready = all.Count(o => o.Status == "Ready"),
            newOrders = all.Count(o => o.Status == "New")
        });
    }



    public async Task<IActionResult> OnPostCreateOrderAsync()
    {
        var lines = (NewOrder.Lines ?? new())
            .Where(l => l.MenuItemId > 0)
            .Select(l => new OrderLineInput(l.MenuItemId, l.Quantity))
            .ToList();

        if (lines.Count == 0)
        {
            TempData["ErrorMessage"] = "Add at least one dish before submitting the order.";
            return RedirectToPage();
        }

        // The logged-in staff member is recorded as the person who entered the order.
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : (int?)null;
        if (!userId.HasValue)
        {
            TempData["ErrorMessage"] = "Your account could not be identified. Please sign in again.";
            return RedirectToPage();
        }

        var result = await _orders.CreateAsync(new CreateOrderInput(
            NewOrder.CustomerId, NewOrder.TableId, null, lines, userId));
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }

    // Moves an order along: New -> Cooking -> Ready -> Completed, or Cancelled.
    // The service checks that the move is allowed.
    public async Task<IActionResult> OnPostUpdateStatusAsync(int orderId, string newStatus)
    {
        var result = await _orders.UpdateStatusAsync(orderId, newStatus);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }
}