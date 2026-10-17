using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Services;

namespace ResROMS.Pages.Admin;

public class RestaurantInfoModel : PageModel
{
    private readonly IRestaurantManagementService _restaurant;
    private readonly AppDbContext _db;

    public RestaurantInfoModel(IRestaurantManagementService restaurant, AppDbContext db)
    {
        _restaurant = restaurant;
        _db = db;
    }

    public class GeneralInfoInput
    {
        [Required, StringLength(120)]
        [Display(Name = "Restaurant Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(40), Phone]
        [Display(Name = "Contact Number")]
        public string Phone { get; set; } = string.Empty;

        [StringLength(254), EmailAddress]
        [Display(Name = "Support Email")]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(500)]
        [Display(Name = "Restaurant Address")]
        public string Address { get; set; } = string.Empty;
    }

    public class OperatingHourInput
    {
        public int DayOfWeek { get; set; }
        public bool IsClosed { get; set; }
        [DataType(DataType.Time)] public TimeOnly OpeningTime { get; set; } = new(9, 0);
        [DataType(DataType.Time)] public TimeOnly ClosingTime { get; set; } = new(22, 0);
    }

    public sealed record DiningAreaView(string Name, int TotalTables, int Capacity);
    public sealed record CategoryView(string Name, int ItemCount);

    // These properties render asp-for fields. Only the selected handler's
    // parameter is model-bound so unrelated tab fields are not required on POST.
    public GeneralInfoInput Input { get; private set; } = new();
    public List<OperatingHourInput> Hours { get; private set; } = [];
    public IReadOnlyList<DiningAreaView> DiningAreas { get; private set; } = [];
    public IReadOnlyList<CategoryView> Categories { get; private set; } = [];
    public string ActiveTab { get; private set; } = "general";

    public async Task OnGetAsync(string? tab)
    {
        ActiveTab = tab is "hours" or "areas" or "categories" ? tab : "general";
        await LoadDetailsAsync();
        await LoadHoursAsync();
        await LoadRelatedAsync();
    }

    public async Task<IActionResult> OnPostSaveInfoAsync([Bind(Prefix = "Input")] GeneralInfoInput input)
    {
        ActiveTab = "general";
        Input = input;
        if (ModelState.IsValid)
        {
            var result = await _restaurant.SaveDetailsAsync(new RestaurantDetails(
                input.Name, input.Phone, input.Email, input.Address));
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToPage(new { tab = "general" });
            }
            ModelState.AddModelError(string.Empty, result.Message);
        }
        await LoadHoursAsync();
        await LoadRelatedAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveHoursAsync([Bind(Prefix = "Hours")] List<OperatingHourInput> hours)
    {
        ActiveTab = "hours";
        Hours = hours;
        if (ModelState.IsValid)
        {
            var result = await _restaurant.SaveHoursAsync(hours.Select(h => new WeeklyHours(
                h.DayOfWeek, h.IsClosed, h.OpeningTime, h.ClosingTime)).ToList());
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToPage(new { tab = "hours" });
            }
            ModelState.AddModelError(string.Empty, result.Message);
        }
        await LoadDetailsAsync();
        await LoadRelatedAsync();
        return Page();
    }

    private async Task LoadDetailsAsync()
    {
        var details = await _restaurant.GetDetailsAsync();
        Input = new GeneralInfoInput
        {
            Name = details.Name, Phone = details.Phone, Email = details.Email, Address = details.Address
        };
    }

    private async Task LoadHoursAsync()
    {
        var hours = await _restaurant.GetHoursAsync();
        // Monday first in the Admin UI, but SQL stores .NET DayOfWeek 0..6.
        Hours = hours.OrderBy(h => (h.DayOfWeek + 6) % 7)
            .Select(h => new OperatingHourInput
            {
                DayOfWeek = h.DayOfWeek, IsClosed = h.IsClosed,
                OpeningTime = h.OpeningTime, ClosingTime = h.ClosingTime
            }).ToList();
    }

    private async Task LoadRelatedAsync()
    {
        DiningAreas = await _db.DiningAreas.AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new DiningAreaView(a.Name, a.Tables.Count(),
                a.Tables.Sum(t => (int?)t.Capacity) ?? 0))
            .ToListAsync();
        Categories = await _db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryView(c.Name, c.MenuItems.Count))
            .ToListAsync();
    }
}
