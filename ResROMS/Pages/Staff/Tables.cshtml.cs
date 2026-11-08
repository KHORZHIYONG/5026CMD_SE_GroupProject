using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages.Staff;

// Floor plan for the waiter. Seating a table marks it Occupied, then the waiter
// either takes the order now or leaves it for later.
// It only calls the shared services, so Admin sees the same table status.
public sealed class TablesModel : PageModel
{
    private readonly ITableManagementService _tables;
    private readonly IOrderManagementService _orders;
    private readonly IReservationService _reservations;

    public TablesModel(ITableManagementService tables, IOrderManagementService orders,
        IReservationService reservations)
    {
        _tables = tables;
        _orders = orders;
        _reservations = reservations;
    }

    public IReadOnlyList<DiningAreaSummary> Areas { get; private set; } = [];
    public IReadOnlyList<TableSummary> Tables { get; private set; } = [];

    // Latest unfinished order for each table number (New, Cooking or Ready).
    public Dictionary<string, OrderSummary> ActiveByTable { get; private set; } = new();

    // Bookings made on the Reservations page that are still ahead of us (Pending or Confirmed),
    // grouped by the table they were booked for.
    public Dictionary<int, List<ReservationSummary>> ReservationsByTable { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Areas = await _tables.GetAreasAsync();
        Tables = await _tables.GetTablesAsync();

        var now = ReservationClock.NowLocal;
        var bookings = await _reservations.GetForAdminAsync(null);
        foreach (var b in bookings
                     .Where(b => (b.Status is "Pending" or "Confirmed") && b.EndLocal >= now)
                     .OrderBy(b => b.StartLocal))
        {
            if (!ReservationsByTable.TryGetValue(b.TableId, out var list))
                ReservationsByTable[b.TableId] = list = new List<ReservationSummary>();
            list.Add(b);
        }

        var all = await _orders.GetOrdersAsync(); // newest first
        foreach (var o in all.Where(o => o.Status is "New" or "Cooking" or "Ready"))
            ActiveByTable.TryAdd(o.TableLabel, o); // keeps the newest one only
    }

    // next = "order" opens the menu, anything else just seats the guests.
    public async Task<IActionResult> OnPostSeatAsync(int tableId, int guests, string next)
    {
        if (guests < 1 || guests > 50)
        {
            TempData["ErrorMessage"] = "Enter the number of guests (1 to 50).";
            return RedirectToPage();
        }

        var result = await _tables.ChangeStatusAsync(tableId, "Occupied");
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToPage();
        }

        if (next == "order")
            return RedirectToPage("/Staff/TakeOrder", new { tableId, guests });

        TempData["SuccessMessage"] = result.Message + " Order can be taken later.";
        return RedirectToPage();
    }

    // The waiter picks a booking for this table. The table turns Reserved (blue).
    public async Task<IActionResult> OnPostReserveAsync(int tableId, int reservationId)
    {
        var booking = await _reservations.FindAsync(reservationId);
        if (booking is null || booking.TableId != tableId ||
            booking.Status is not ("Pending" or "Confirmed"))
        {
            TempData["ErrorMessage"] = "That reservation is not available for this table.";
            return RedirectToPage();
        }

        var result = await _tables.ChangeStatusAsync(tableId, "Reserved");
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Success
            ? $"Table {booking.TableNumber} is now Reserved for {booking.CustomerName}."
            : result.Message;
        return RedirectToPage();
    }

    // After the guests leave and the bill is paid, the waiter frees the table.
    public async Task<IActionResult> OnPostFreeAsync(int tableId)
    {
        var result = await _tables.ChangeStatusAsync(tableId, "Available");
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }
}
