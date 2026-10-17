using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace ResROMS.Pages.Customer;

public sealed class ReservationsModel : PageModel
{
    private readonly IReservationService _reservations;
    private readonly IAdminSettingsService _settings;
    public ReservationsModel(IReservationService reservations, IAdminSettingsService settings)
    {
        _reservations = reservations;
        _settings = settings;
    }
    public bool CustomerBookingsEnabled { get; private set; } = true;

    public sealed class BookingFields
    {
        [Range(1, int.MaxValue)] public int TableId { get; set; }
        [Required] public DateTime StartLocal { get; set; }
        [Range(30, 240)] public int DurationMinutes { get; set; } = 90;
        [Range(1, 20)] public int PartySize { get; set; } = 2;
        [StringLength(500)] public string? SpecialRequests { get; set; }
    }

    public IReadOnlyList<ReservationSummary> Bookings { get; private set; } = [];
    public IReadOnlyList<ReservationTableOption> Tables { get; private set; } = [];
    public BookingFields NewBooking { get; private set; } = new();
    public BookingFields EditBooking { get; private set; } = new();
    public ReservationSummary? Editing { get; private set; }
    public string CustomerName => User.Identity?.Name ?? "Customer";

    private int CustomerId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnGetAsync(int? editId = null)
    {
        if (CustomerId == 0) return Challenge();
        var policy = await _settings.GetAsync();
        CustomerBookingsEnabled = policy.CustomerBookingsEnabled;
        Bookings = await _reservations.GetForCustomerAsync(CustomerId);
        Tables = await _reservations.GetTablesAsync();
        var next = ReservationClock.NowLocal.AddHours(1);
        NewBooking.StartLocal = new DateTime(next.Year, next.Month, next.Day, next.Hour, 0, 0)
            .AddMinutes((next.Minute / 15 + 1) * 15);
        if (editId.HasValue)
        {
            Editing = await _reservations.FindAsync(editId.Value, CustomerId);
            if (Editing is not null)
            {
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
        return Page();
    }

    public async Task<IActionResult> OnGetAvailabilityAsync(DateTime startLocal,
        int durationMinutes, int partySize)
    {
        var options = await _reservations.FindAvailableTablesAsync(startLocal, durationMinutes, partySize, forCustomer: true);
        return new JsonResult(options.Select(t => new
        {
            id = t.Id,
            label = $"{t.TableNumber} · {t.AreaName} · {t.Capacity} seats"
        }));
    }

    public async Task<IActionResult> OnPostCreateAsync([Bind(Prefix = "NewBooking")] BookingFields input)
    {
        if (CustomerId == 0) return Challenge();
        if (!ModelState.IsValid) return InvalidInput();
        return Finish(await _reservations.CreateForCustomerAsync(CustomerId, Convert(input)));
    }

    public async Task<IActionResult> OnPostEditAsync(int id, [Bind(Prefix = "EditBooking")] BookingFields input)
    {
        if (CustomerId == 0) return Challenge();
        if (!ModelState.IsValid) return InvalidInput();
        return Finish(await _reservations.UpdateAsync(id, CustomerId, Convert(input)));
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        if (CustomerId == 0) return Challenge();
        return Finish(await _reservations.CancelAsync(id, CustomerId));
    }

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
        TempData["ErrorMessage"] = ModelState.Values.SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage).FirstOrDefault() ?? "Please check your booking details.";
        return RedirectToPage();
    }
}
