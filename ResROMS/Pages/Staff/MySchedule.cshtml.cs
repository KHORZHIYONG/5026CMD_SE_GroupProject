using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;
using ResROMS.Services;

namespace ResROMS.Pages.Staff;

/// <summary>Shows a staff member only their own assigned shifts. Admin remains the only editor.</summary>
public sealed class MyScheduleModel : PageModel
{
    private readonly AppDbContext _db;
    public MyScheduleModel(AppDbContext db) => _db = db;

    public DateOnly Monday { get; private set; }
    public DateOnly PreviousMonday => Monday.AddDays(-7);
    public DateOnly NextMonday => Monday.AddDays(7);
    public IReadOnlyList<StaffShift> Shifts { get; private set; } = Array.Empty<StaffShift>();
    public IReadOnlyList<ScheduleDay> Days { get; private set; } = [];
    public int GridStartHour { get; private set; } = 8;
    public int GridEndHour { get; private set; } = 21;
    public int GridHeightPx => (GridEndHour - GridStartHour) * 52;
    public IReadOnlyList<int> Hours => Enumerable.Range(GridStartHour, GridEndHour - GridStartHour + 1).ToList();
    public double WeeklyHours { get; private set; }
    public sealed record ScheduleSegment(DateTime StartLocal, DateTime EndLocal,
        bool ContinuesFromPriorDay, bool ContinuesToNextDay, string Notes,
        double TopPixels, double HeightPixels);
    public sealed record ScheduleDay(DateOnly Date, IReadOnlyList<ScheduleSegment> Blocks);

    public async Task<IActionResult> OnGetAsync(string? week, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId))
            return Challenge();

        var selected = DateOnly.TryParseExact(week, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var value) && value.Year >= 2020 && value.Year <= 2099
            ? value : DateOnly.FromDateTime(ReservationClock.NowLocal);
        Monday = StaffSchedulingService.MondayOf(selected);
        var start = Monday.ToDateTime(TimeOnly.MinValue);
        var end = Monday.AddDays(7).ToDateTime(TimeOnly.MinValue);
        Shifts = await _db.StaffShifts.AsNoTracking()
            .Where(x => x.StaffUserId == staffId && x.StartLocal < end && x.EndLocal > start)
            .OrderBy(x => x.StartLocal).ToListAsync(ct);

        // Include split portions of shifts crossing midnight or the week edge.
        // The timeline uses local time, just like the Administrator schedule.
        var clipped = Shifts.Select(x => (
            Shift: x,
            Begin: x.StartLocal > start ? x.StartLocal : start,
            Finish: x.EndLocal < end ? x.EndLocal : end)).ToArray();
        WeeklyHours = clipped.Sum(x => (x.Finish - x.Begin).TotalHours);
        var raw = new List<(DateOnly Day, StaffShift Shift, DateTime Begin, DateTime Finish)>();
        for (int i = 0; i < 7; i++)
        {
            var date = Monday.AddDays(i);
            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            foreach (var shift in Shifts)
            {
                var begin = shift.StartLocal > dayStart ? shift.StartLocal : dayStart;
                var finish = shift.EndLocal < dayEnd ? shift.EndLocal : dayEnd;
                if (begin < finish) raw.Add((date, shift, begin, finish));
            }
        }
        if (raw.Count > 0)
        {
            GridStartHour = Math.Max(0, Math.Min(8,
                (int)Math.Floor(raw.Min(x => x.Begin.TimeOfDay.TotalHours))));
            GridEndHour = Math.Min(24, Math.Max(21,
                (int)Math.Ceiling(raw.Max(x => x.Finish.TimeOfDay.TotalHours))));
            // Midnight ending (00:00 next day) is a full 24:00 for the previous day.
            if (raw.Any(x => x.Finish.Date > x.Begin.Date ||
                (x.Finish.Hour == 0 && x.Finish.Date > x.Day.ToDateTime(TimeOnly.MinValue).Date)))
                GridEndHour = 24;
        }
        Days = Enumerable.Range(0, 7).Select(i =>
        {
            var date = Monday.AddDays(i);
            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var items = raw.Where(x => x.Day == date).Select(x => new ScheduleSegment(
                x.Begin, x.Finish,
                x.Shift.StartLocal < dayStart, x.Shift.EndLocal > dayStart.AddDays(1),
                x.Shift.Notes,
                (x.Begin - dayStart).TotalMinutes / 60d * 52d - GridStartHour * 52d,
                Math.Max(22d, (x.Finish - x.Begin).TotalMinutes / 60d * 52d))).ToArray();
            return new ScheduleDay(date, items);
        }).ToArray();
        return Page();
    }
}
