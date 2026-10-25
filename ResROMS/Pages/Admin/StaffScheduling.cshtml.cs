using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public sealed class StaffSchedulingModel : PageModel
{
    private readonly IStaffSchedulingService _scheduling;
    public StaffSchedulingModel(IStaffSchedulingService scheduling) => _scheduling = scheduling;

    public StaffWeek Board { get; private set; } = null!;
    public DateOnly CurrentMonday { get; private set; }
    public DateOnly PreviousWeek => CurrentMonday.AddDays(-7);
    public DateOnly NextWeek => CurrentMonday.AddDays(7);
    public List<DateOnly> Days => Enumerable.Range(0, 7)
        .Select(i => CurrentMonday.AddDays(i)).ToList();

    public sealed class ShiftInput
    {
        public int ShiftId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Select a staff member.")]
        public int StaffUserId { get; set; }
        public DateOnly WorkDate { get; set; }
        [DataType(DataType.Time)] public TimeOnly StartTime { get; set; } = new(10, 0);
        [DataType(DataType.Time)] public TimeOnly EndTime { get; set; } = new(18, 0);
        [StringLength(300)] public string Notes { get; set; } = string.Empty;
    }

    [BindProperty] public ShiftInput Form { get; set; } = new();

    public async Task OnGetAsync(string? week, CancellationToken ct)
    {
        await LoadAsync(week, ct);
        Form.WorkDate = CurrentMonday;
    }

    public async Task<IActionResult> OnPostSaveAsync(string? week, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync(week, ct);
            return Page();
        }
        var start = Form.WorkDate.ToDateTime(Form.StartTime);
        var end = Form.WorkDate.ToDateTime(Form.EndTime);
        if (end <= start) end = end.AddDays(1); // overnight shifts
        var result = await _scheduling.SaveAsync(Form.ShiftId, Form.StaffUserId,
            start, end, Form.Notes, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await LoadAsync(week, ct);
            return Page();
        }
        TempData["SuccessMessage"] = result.Message;
        return RedirectToPage(new { week = WeekValue(week) });
    }

    public async Task<IActionResult> OnPostDropAsync(int shiftId, int staffId,
        DateOnly targetDate, TimeOnly startTime, TimeOnly endTime, string? week, CancellationToken ct)
    {
        OperationResult result;
        if (shiftId > 0) result = await _scheduling.MoveAsync(shiftId, targetDate, ct);
        else
        {
            var start = targetDate.ToDateTime(startTime);
            var end = targetDate.ToDateTime(endTime);
            if (end <= start) end = end.AddDays(1);
            result = await _scheduling.SaveAsync(0, staffId, start, end, null, ct);
        }
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage(new { week = WeekValue(week) });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int shiftId, string? week, CancellationToken ct)
    {
        var result = await _scheduling.DeleteAsync(shiftId, ct);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage(new { week = WeekValue(week) });
    }

    private async Task LoadAsync(string? week, CancellationToken ct)
    {
        var chosen = DateOnly.TryParseExact(week, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed : DateOnly.FromDateTime(ReservationClock.ToLocal(DateTime.UtcNow));
        if (chosen.Year < 2020 || chosen.Year > 2099)
            chosen = DateOnly.FromDateTime(ReservationClock.ToLocal(DateTime.UtcNow));
        CurrentMonday = StaffSchedulingService.MondayOf(chosen);
        Board = await _scheduling.GetWeekAsync(CurrentMonday, ct);
    }

    private string WeekValue(string? week) =>
        DateOnly.TryParseExact(week, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? StaffSchedulingService.MondayOf(parsed).ToString("yyyy-MM-dd")
            : StaffSchedulingService.MondayOf(DateOnly.FromDateTime(
                ReservationClock.ToLocal(DateTime.UtcNow))).ToString("yyyy-MM-dd");
}
