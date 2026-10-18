using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;
using System.ComponentModel.DataAnnotations;

namespace ResROMS.Pages.Staff;

// Staff Reservations page. Same rules as the Admin page, because both call
// IReservationService. A booking is made for a Customer account.
public sealed class ReservationsModel : PageModel
{
    private readonly IReservationService _reservations;
    public ReservationsModel(IReservationService reservations) => _reservations = reservations;

    public sealed class BookingFields
    {
        [Range(1, int.MaxValue, ErrorMessage = "Choose a table.")]
        public int TableId { get; set; }
        [Required]
        public DateTime StartLocal { get; set; }
        [Range(30, 240)]
        public int DurationMinutes { get; set; } = 90;
        [Range(1, 20)]
        public int PartySize { get; set; } = 2;
        [StringLength(500)]
        public string? SpecialRequests { get; set; }
    }

    public IReadOnlyList<ReservationSummary> Bookings { get; private set; } = [];
    public IReadOnlyList<ReservationTableOption> Tables { get; private set; } = [];
    public IReadOnlyList<ReservationCustomerOption> Customers { get; private set; } = [];
    public BookingFields NewBooking { get; private set; } = new();
    public BookingFields EditBooking { get; private set; } = new();
    public ReservationSummary? Editing { get; private set; }
    public DateTime? SelectedDate { get; private set; }

    public async Task OnGetAsync(DateTime? date = null, int? editId = null)
    {
        SelectedDate = date;
        Bookings = await _reservations.GetForAdminAsync(date);
        Tables = await _reservations.GetTablesAsync();
        Customers = await _reservations.GetCustomersAsync();
        NewBooking.StartLocal = NextQuarterHour();
        if (editId.HasValue)
        {
            Editing = await _reservations.FindAsync(editId.Value);
            if (Editing != null)
                EditBooking = new BookingFields
                {
                    TableId = Editing.TableId,
                    StartLocal = Editing.StartLocal,
                    DurationMinutes = (int)(Editing.EndLocal - Editing.StartLocal).TotalMinutes,
                    PartySize = Editing.PartySize,
                    SpecialRequests = Editing.SpecialRequests
                };
        }
    }

    public async Task<IActionResult> OnGetAvailabilityAsync(DateTime startLocal,
        int durationMinutes, int partySize)
    {
        var tables = await _reservations.FindAvailableTablesAsync(startLocal, durationMinutes, partySize);
        return new JsonResult(tables.Select(t => new
        {
            id = t.Id,
            label = $"{t.TableNumber} · {t.AreaName} · {t.Capacity} seats"
        }));
    }

    public async Task<IActionResult> OnPostCreateAsync(int customerId,
        [Bind(Prefix = "NewBooking")] BookingFields input)
    {
        if (!ModelState.IsValid) return InvalidInput();
        return Finish(await _reservations.CreateForAdminAsync(customerId, Convert(input)));
    }

    public async Task<IActionResult> OnPostEditAsync(int id,
        [Bind(Prefix = "EditBooking")] BookingFields input)
    {
        if (!ModelState.IsValid) return InvalidInput();
        return Finish(await _reservations.UpdateAsync(id, null, Convert(input)));
    }

    public async Task<IActionResult> OnPostConfirmAsync(int id) =>
        Finish(await _reservations.ConfirmAsync(id));

    public async Task<IActionResult> OnPostCompleteAsync(int id) =>
        Finish(await _reservations.CompleteAsync(id));

    public async Task<IActionResult> OnPostCancelAsync(int id) =>
        Finish(await _reservations.CancelAsync(id, null));

    private static ReservationRequest Convert(BookingFields input) =>
        new(input.TableId, input.StartLocal, input.DurationMinutes,
            input.PartySize, input.SpecialRequests);

    private IActionResult Finish(ReservationResult result)
    {
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }

    private IActionResult InvalidInput()
    {
        var message = ModelState.Values.SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage).FirstOrDefault();
        TempData["ErrorMessage"] = message ?? "Please check the reservation details.";
        return RedirectToPage();
    }

    private static DateTime NextQuarterHour()
    {
        var next = ReservationClock.NowLocal.AddMinutes(45);
        return new DateTime(next.Year, next.Month, next.Day, next.Hour, 0, 0)
            .AddMinutes((next.Minute / 15 + 1) * 15);
    }
}