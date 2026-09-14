using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ResROMS.Pages.Admin
{
    public class TablesModel : PageModel
    {
        // Sample ViewModel for Area
        public class DiningAreaDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int TableCount { get; set; }
        }

        // Sample ViewModel for Table
        public class TableDto
        {
            public int Id { get; set; }
            public string TableNumber { get; set; } = string.Empty;
            public int Capacity { get; set; }
            public int AreaId { get; set; }
            public string Status { get; set; } = "Available"; // Available, Occupied, Reserved, Maintenance
            public string Shape { get; set; } = "Square"; // Square, Round, Rectangle
        }

        public List<DiningAreaDto> DiningAreas { get; set; } = new();
        public List<TableDto> Tables { get; set; } = new();

        [BindProperty]
        public TableDto NewTable { get; set; } = new();

        [BindProperty]
        public DiningAreaDto NewArea { get; set; } = new();

        public void OnGet()
        {
            // Dummy Data for Previewing UI
            DiningAreas = new List<DiningAreaDto>
            {
                new DiningAreaDto { Id = 1, Name = "Main Dining Room", TableCount = 6 },
                new DiningAreaDto { Id = 2, Name = "Outdoor Terrace", TableCount = 4 },
                new DiningAreaDto { Id = 3, Name = "VIP Lounge", TableCount = 2 }
            };

            Tables = new List<TableDto>
            {
                new TableDto { Id = 101, TableNumber = "T-01", Capacity = 2, AreaId = 1, Status = "Occupied", Shape = "Square" },
                new TableDto { Id = 102, TableNumber = "T-02", Capacity = 4, AreaId = 1, Status = "Available", Shape = "Square" },
                new TableDto { Id = 103, TableNumber = "T-03", Capacity = 4, AreaId = 1, Status = "Reserved", Shape = "Round" },
                new TableDto { Id = 104, TableNumber = "T-04", Capacity = 6, AreaId = 1, Status = "Available", Shape = "Rectangle" },
                new TableDto { Id = 105, TableNumber = "T-05", Capacity = 2, AreaId = 1, Status = "Maintenance", Shape = "Square" },
                new TableDto { Id = 106, TableNumber = "T-06", Capacity = 8, AreaId = 1, Status = "Available", Shape = "Rectangle" },
                new TableDto { Id = 201, TableNumber = "P-01", Capacity = 4, AreaId = 2, Status = "Available", Shape = "Round" },
                new TableDto { Id = 202, TableNumber = "P-02", Capacity = 4, AreaId = 2, Status = "Occupied", Shape = "Round" }
            };
        }

        public IActionResult OnPostAddTable()
        {
            // Backend saving logic goes here later
            TempData["SuccessMessage"] = $"Table '{NewTable.TableNumber}' created successfully!";
            return RedirectToPage();
        }

        public IActionResult OnPostAddArea()
        {
            // Backend saving logic goes here later
            TempData["SuccessMessage"] = $"Area '{NewArea.Name}' created successfully!";
            return RedirectToPage();
        }
    }
}