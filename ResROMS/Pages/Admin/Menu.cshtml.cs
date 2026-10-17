using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;

namespace ResROMS.Pages.Admin
{
    public class MenuModel : PageModel
    {
        private readonly AppDbContext _context;

        public MenuModel(AppDbContext context)
        {
            _context = context;
        }

        // Using real database models now instead of DTOs
        public IList<Category> Categories { get; set; } = default!;
        public IList<MenuItem> MenuItems { get; set; } = default!;

        [BindProperty]
        public MenuItem NewMenuItem { get; set; } = default!;

        [BindProperty]
        public Category NewCategory { get; set; } = default!;

        // --- ADD THESE FOR EDITING ---
        [BindProperty]
        public MenuItem EditMenuItem { get; set; } = default!;


        // --- DELETE METHOD ---
        public async Task<IActionResult> OnPostDeleteMenuItemAsync(int id)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item != null)
            {
                if (await _context.OrderItems.AnyAsync(oi => oi.MenuItemId == id))
                {
                    TempData["ErrorMessage"] = "This dish appears in order history. Mark it unavailable instead of deleting it.";
                    return RedirectToPage();
                }
                _context.MenuItems.Remove(item);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Dish '{item.Name}' removed from the menu.";
            }
            return RedirectToPage();
        }

        // --- EDIT METHOD ---
        public async Task<IActionResult> OnPostEditMenuItemAsync()
        {
            var itemInDb = await _context.MenuItems.FindAsync(EditMenuItem.Id);
            if (itemInDb != null)
            {
                // Update the existing item with the new values
                itemInDb.Name = EditMenuItem.Name;
                itemInDb.Price = EditMenuItem.Price;
                itemInDb.CategoryId = EditMenuItem.CategoryId;
                itemInDb.DietaryTag = EditMenuItem.DietaryTag;
                itemInDb.ImageUrl = EditMenuItem.ImageUrl;
                itemInDb.Description = EditMenuItem.Description;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Dish '{itemInDb.Name}' updated successfully!";
            }
            return RedirectToPage();
        }

        public async Task OnGetAsync()
        {
            // Fetch live data from LocalDB
            Categories = await _context.Categories.ToListAsync();
            MenuItems = await _context.MenuItems.Include(m => m.Category).ToListAsync();
        }

        public async Task<IActionResult> OnPostAddMenuItemAsync()
        {
            _context.MenuItems.Add(NewMenuItem);
            await _context.SaveChangesAsync(); // Saves permanently to LocalDB

            TempData["SuccessMessage"] = $"Dish '{NewMenuItem.Name}' added to menu!";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddCategoryAsync()
        {
            _context.Categories.Add(NewCategory);
            await _context.SaveChangesAsync(); // Saves permanently to LocalDB

            TempData["SuccessMessage"] = $"Category '{NewCategory.Name}' created!";
            return RedirectToPage();
        }

        // --- THIS RUNS WHEN YOU TOGGLE THE SWITCH ---
        public async Task<IActionResult> OnPostToggleAvailabilityAsync(int id, bool isAvailable)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            item.IsAvailable = isAvailable;
            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true });
        }
    }
}