using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Models;
using ResROMS.Services;

namespace ResROMS.Pages.Admin;

public class MenuModel : PageModel
{
    private readonly IMenuManagementService _menu;
    public MenuModel(IMenuManagementService menu) => _menu = menu;

    public class MenuItemForm
    {
        public int Id { get; set; }

        [Required, StringLength(120, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "99999.99", ErrorMessage = "Enter a price from RM 0.01 to RM 99,999.99.")]
        public decimal Price { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
        public int CategoryId { get; set; }

        public string? DietaryTag { get; set; }
        [StringLength(2048)] public string? ImageUrl { get; set; }
        [StringLength(2000)] public string? Description { get; set; }

        public MenuItemChange ToChange() =>
            new(Name, Price, CategoryId, DietaryTag, ImageUrl, Description);
    }

    public class CategoryForm
    {
        [Required, StringLength(80, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;
    }

    // For asp-for tag helpers. Actual binding happens per handler, so posting
    // one modal does not validate unrelated modal forms.
    public MenuItemForm NewMenuItem { get; private set; } = new();
    public MenuItemForm EditMenuItem { get; private set; } = new();
    public CategoryForm NewCategory { get; private set; } = new();

    public IReadOnlyList<Category> Categories { get; private set; } = [];
    public IReadOnlyList<MenuItem> MenuItems { get; private set; } = [];
    public string? ActiveModal { get; private set; }

    public async Task OnGetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var catalog = await _menu.GetCatalogAsync();
        Categories = catalog.Categories;
        MenuItems = catalog.MenuItems;
    }

    private IActionResult Result(OperationResult result)
    {
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }

    private async Task<IActionResult> InvalidAsync(string modal)
    {
        ActiveModal = modal;
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAddMenuItemAsync(
        [Bind(Prefix = "NewMenuItem")] MenuItemForm input)
    {
        NewMenuItem = input;
        if (!ModelState.IsValid) return await InvalidAsync("addItemModal");
        return Result(await _menu.AddMenuItemAsync(input.ToChange()));
    }

    public async Task<IActionResult> OnPostEditMenuItemAsync(
        [Bind(Prefix = "EditMenuItem")] MenuItemForm input)
    {
        EditMenuItem = input;
        if (input.Id <= 0)
            ModelState.AddModelError("EditMenuItem.Id", "Invalid menu item.");
        if (!ModelState.IsValid) return await InvalidAsync("editItemModal");
        return Result(await _menu.EditMenuItemAsync(input.Id, input.ToChange()));
    }

    public async Task<IActionResult> OnPostDeleteMenuItemAsync(int id) =>
        Result(await _menu.DeleteMenuItemAsync(id));

    public async Task<IActionResult> OnPostToggleAvailabilityAsync(int id, bool isAvailable)
    {
        var result = await _menu.SetAvailabilityAsync(id, isAvailable);
        return result.Success
            ? new JsonResult(new { success = true })
            : new BadRequestObjectResult(new { success = false, error = result.Message });
    }

    public async Task<IActionResult> OnPostAddCategoryAsync(
        [Bind(Prefix = "NewCategory")] CategoryForm input)
    {
        NewCategory = input;
        if (!ModelState.IsValid) return await InvalidAsync("addCategoryModal");
        return Result(await _menu.AddCategoryAsync(input.Name));
    }

    public async Task<IActionResult> OnPostRenameCategoryAsync(int id,
        [Bind(Prefix = "RenameCategory")] CategoryForm input)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Category name is required and must be at most 80 characters.";
            return RedirectToPage();
        }
        return Result(await _menu.RenameCategoryAsync(id, input.Name));
    }

    public async Task<IActionResult> OnPostDeleteCategoryAsync(int id) =>
        Result(await _menu.DeleteCategoryAsync(id));
}
