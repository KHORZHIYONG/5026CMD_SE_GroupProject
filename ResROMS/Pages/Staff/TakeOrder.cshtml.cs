using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Models;
using ResROMS.Services;

namespace ResROMS.Pages.Staff;

// Menu on the left, bill on the right. The bill is built in the browser,
// then posted once when the waiter confirms. The shared order service saves it.
public sealed class TakeOrderModel : PageModel
{
    private readonly IOrderManagementService _orders;
    private readonly IMenuManagementService _menu;

    public TakeOrderModel(IOrderManagementService orders, IMenuManagementService menu)
    {
        _orders = orders;
        _menu = menu;
    }

    [BindProperty(SupportsGet = true)] public int TableId { get; set; }
    [BindProperty(SupportsGet = true)] public int Guests { get; set; } = 1;

    public string TableLabel { get; private set; } = "";
    public IReadOnlyList<Category> Categories { get; private set; } = [];
    public IReadOnlyList<MenuItem> Items { get; private set; } = [];

    public sealed class LineInput
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
    }

    [BindProperty] public List<LineInput> Lines { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return RedirectToPage("/Staff/Tables");
        return Page();
    }

    public async Task<IActionResult> OnPostSendAsync()
    {
        var lines = (Lines ?? new())
            .Where(l => l.MenuItemId > 0 && l.Quantity > 0)
            .Select(l => new OrderLineInput(l.MenuItemId, l.Quantity))
            .ToList();

        if (lines.Count == 0)
        {
            TempData["ErrorMessage"] = "Add at least one dish before sending the order.";
            return RedirectToPage(new { TableId, Guests });
        }

        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : (int?)null;
        if (!userId.HasValue)
        {
            TempData["ErrorMessage"] = "Your account could not be identified. Please sign in again.";
            return RedirectToPage("/Staff/Tables");
        }

        var result = await _orders.CreateAsync(new CreateOrderInput(null, TableId, null, lines, userId));
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return result.Success
            ? RedirectToPage("/Staff/Tables")
            : RedirectToPage(new { TableId, Guests });
    }

    private async Task<bool> LoadAsync()
    {
        var table = (await _orders.GetTableChoicesAsync()).FirstOrDefault(t => t.Id == TableId);
        if (table is null)
        {
            TempData["ErrorMessage"] = "That table could not be found.";
            return false;
        }
        TableLabel = table.Label;

        var catalog = await _menu.GetCatalogAsync();
        Categories = catalog.Categories;
        Items = catalog.MenuItems.Where(i => i.IsAvailable).OrderBy(i => i.Name).ToList();
        return true;
    }
}
