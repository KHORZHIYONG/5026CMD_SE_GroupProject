using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ResROMS.Pages.Admin
{
    public class CampaignsModel : PageModel
    {
        public class CampaignDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string PromoCode { get; set; } = string.Empty;
            public string Type { get; set; } = "Discount"; // Discount, Happy Hour, Seasonal
            public string Status { get; set; } = "Active"; // Active, Scheduled, Ended
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string Description { get; set; } = string.Empty;
            public int UsageCount { get; set; }
        }

        public class CreateCampaignDto
        {
            public string Name { get; set; } = string.Empty;
            public string PromoCode { get; set; } = string.Empty;
            public string Type { get; set; } = "Discount";
            public DateTime StartDate { get; set; } = DateTime.Today;
            public DateTime EndDate { get; set; } = DateTime.Today.AddDays(30);
            public string Description { get; set; } = string.Empty;
        }

        public List<CampaignDto> Campaigns { get; set; } = new();

        [BindProperty]
        public CreateCampaignDto NewCampaign { get; set; } = new();

        public void OnGet()
        {
            Campaigns = new List<CampaignDto>
            {
                new CampaignDto {
                    Id = 1,
                    Name = "Summer Happy Hour",
                    PromoCode = "N/A (Auto-applied)",
                    Type = "Happy Hour",
                    Status = "Active",
                    StartDate = DateTime.Now.AddDays(-10),
                    EndDate = DateTime.Now.AddDays(20),
                    Description = "50% off all beverages between 4 PM and 6 PM.",
                    UsageCount = 142
                },
                new CampaignDto {
                    Id = 2,
                    Name = "Welcome Discount",
                    PromoCode = "WELCOME10",
                    Type = "Discount",
                    Status = "Active",
                    StartDate = new DateTime(2025, 1, 1),
                    EndDate = new DateTime(2027, 12, 31),
                    Description = "10% off the first order for newly registered customers.",
                    UsageCount = 356
                },
                new CampaignDto {
                    Id = 3,
                    Name = "Valentine's Day Special",
                    PromoCode = "VDAY2027",
                    Type = "Seasonal",
                    Status = "Scheduled",
                    StartDate = new DateTime(2027, 2, 10),
                    EndDate = new DateTime(2027, 2, 15),
                    Description = "Special fixed-price menu for couples with complimentary dessert.",
                    UsageCount = 0
                },
                new CampaignDto {
                    Id = 4,
                    Name = "Winter Warm-Up",
                    PromoCode = "WINTER15",
                    Type = "Seasonal",
                    Status = "Ended",
                    StartDate = new DateTime(2025, 12, 1),
                    EndDate = new DateTime(2026, 2, 28),
                    Description = "15% off all soups and hot appetizers.",
                    UsageCount = 89
                }
            };
        }

        public IActionResult OnPostAddCampaign()
        {
            TempData["SuccessMessage"] = $"Campaign '{NewCampaign.Name}' successfully scheduled!";
            return RedirectToPage();
        }
    }
}