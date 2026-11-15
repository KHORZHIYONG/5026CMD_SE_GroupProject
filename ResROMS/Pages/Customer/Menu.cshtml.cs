using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;

namespace ResROMS.Pages.Customer;

public sealed class MenuModel : PageModel
{
    private readonly AppDbContext _db;
    public MenuModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Dietary { get; set; }
    [BindProperty(SupportsGet = true)] public bool AvailableOnly { get; set; }

    public List<Category> Categories { get; private set; } = new();
    public List<string> DietaryTags { get; private set; } = new();
    public List<MenuItem> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Categories = await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        DietaryTags = await _db.MenuItems.AsNoTracking()
            .Where(m => m.DietaryTag != null && m.DietaryTag != "")
            .Select(m => m.DietaryTag!).Distinct().OrderBy(t => t).ToListAsync();

        var query = _db.MenuItems.AsNoTracking().Include(m => m.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            query = query.Where(m => m.Name.Contains(term) || m.Description.Contains(term));
        }
        if (CategoryId.HasValue) query = query.Where(m => m.CategoryId == CategoryId.Value);
        if (!string.IsNullOrWhiteSpace(Dietary)) query = query.Where(m => m.DietaryTag == Dietary);
        if (AvailableOnly) query = query.Where(m => m.IsAvailable);

        Items = await query.OrderBy(m => m.Category!.Name).ThenBy(m => m.Name).ToListAsync();
    }
}
