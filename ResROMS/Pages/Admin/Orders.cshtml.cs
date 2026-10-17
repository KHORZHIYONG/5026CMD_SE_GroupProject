using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages.Admin;

public sealed class OrdersModel : PageModel
{
    private readonly IOrderManagementService _orders;
    public OrdersModel(IOrderManagementService orders) => _orders = orders;

    public IReadOnlyList<OrderSummary> Orders { get; private set; } = [];
    public IReadOnlyList<MenuChoice> MenuChoices { get; private set; } = [];
    public IReadOnlyList<AccountChoice> CustomerChoices { get; private set; } = [];
    public IReadOnlyList<TableChoice> TableChoices { get; private set; } = [];

    [BindProperty] public ManualOrderForm NewOrder { get; set; } = new();

    public sealed class ManualOrderForm
    {
        public int? CustomerId { get; set; }
        public int? TableId { get; set; }
        public int? Item1 { get; set; }
        public int Qty1 { get; set; } = 1;
        public int? Item2 { get; set; }
        public int Qty2 { get; set; } = 1;
        public int? Item3 { get; set; }
        public int Qty3 { get; set; } = 1;
    }

    public async Task OnGetAsync()
    {
        Orders = await _orders.GetOrdersAsync();
        MenuChoices = await _orders.GetMenuChoicesAsync();
        CustomerChoices = await _orders.GetCustomerChoicesAsync();
        TableChoices = await _orders.GetTableChoicesAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var lines = new List<OrderLineInput>();
        if (NewOrder.Item1.HasValue) lines.Add(new(NewOrder.Item1.Value, NewOrder.Qty1));
        if (NewOrder.Item2.HasValue) lines.Add(new(NewOrder.Item2.Value, NewOrder.Qty2));
        if (NewOrder.Item3.HasValue) lines.Add(new(NewOrder.Item3.Value, NewOrder.Qty3));
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
            ? id : (int?)null;
        if (!userId.HasValue)
        {
            TempData["ErrorMessage"] = "Your account could not be identified. Sign in again.";
            return RedirectToPage();
        }
        var result = await _orders.CreateAsync(new CreateOrderInput(NewOrder.CustomerId,
            NewOrder.TableId, null, lines, userId));
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(int orderId, string newStatus)
    {
        var result = await _orders.UpdateStatusAsync(orderId, newStatus);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }
}
