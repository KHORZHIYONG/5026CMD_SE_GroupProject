using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;

namespace ResROMS.Pages.Admin;

/// <summary>Admin view of customer ratings. Protected by the /Admin folder policy.</summary>
public sealed class FeedbackModel : PageModel
{
    private readonly AppDbContext _db;
    public FeedbackModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int? Rating { get; set; }

    public List<Feedback> Items { get; private set; } = new();
    public int Total { get; private set; }
    public double Average { get; private set; }
    public int[] PerStar { get; private set; } = new int[6]; // index 1..5

    public async Task OnGetAsync()
    {
        var all = await _db.Feedbacks.AsNoTracking().Include(f => f.Customer)
            .OrderByDescending(f => f.CreatedUtc).Take(1000).ToListAsync();

        Total = all.Count;
        Average = all.Count == 0 ? 0 : Math.Round(all.Average(f => f.Rating), 1);
        foreach (var f in all) PerStar[f.Rating]++;

        Items = Rating is >= 1 and <= 5 ? all.Where(f => f.Rating == Rating).ToList() : all;
    }
}
