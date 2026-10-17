using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;

namespace ResROMS.Services;

public sealed class MenuManagementService : IMenuManagementService
{
    private static readonly HashSet<string> AllowedDietaryTags =
        new(StringComparer.Ordinal) { "Vegetarian", "Vegan", "Gluten-Free", "Spicy" };

    private readonly AppDbContext _db;
    private readonly ILogger<MenuManagementService> _logger;

    public MenuManagementService(AppDbContext db, ILogger<MenuManagementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<MenuCatalog> GetCatalogAsync()
    {
        var categories = await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        var menuItems = await _db.MenuItems.AsNoTracking().OrderBy(m => m.Name).ToListAsync();
        return new MenuCatalog(categories, menuItems);
    }

    public async Task<OperationResult> AddMenuItemAsync(MenuItemChange change)
    {
        var problem = await ValidateMenuItemAsync(change);
        if (problem is not null) return Fail(problem);

        var item = new MenuItem { IsAvailable = true };
        ApplyChange(item, change);
        _db.MenuItems.Add(item);
        return await SaveAsync($"Dish '{item.Name}' added to the menu.");
    }

    public async Task<OperationResult> EditMenuItemAsync(int id, MenuItemChange change)
    {
        if (id <= 0) return Fail("Select a valid menu item.");
        var item = await _db.MenuItems.FindAsync(id);
        if (item is null) return Fail("Menu item not found.");
        var problem = await ValidateMenuItemAsync(change);
        if (problem is not null) return Fail(problem);

        ApplyChange(item, change);
        return await SaveAsync($"Dish '{item.Name}' updated.");
    }

    public async Task<OperationResult> DeleteMenuItemAsync(int id)
    {
        var item = await _db.MenuItems.FindAsync(id);
        if (item is null) return Fail("Menu item not found.");

        // OrderItem -> MenuItem is Restrict in AppDbContext; this friendly check
        // is backed up by the foreign key if an order is placed concurrently.
        if (await _db.OrderItems.AnyAsync(oi => oi.MenuItemId == id))
            return Fail("This dish has order history. Mark it unavailable instead of deleting it.");

        _db.MenuItems.Remove(item);
        return await SaveAsync($"Dish '{item.Name}' deleted.");
    }

    public async Task<OperationResult> SetAvailabilityAsync(int id, bool isAvailable)
    {
        var item = await _db.MenuItems.FindAsync(id);
        if (item is null) return Fail("Menu item not found.");
        item.IsAvailable = isAvailable;
        return await SaveAsync("Availability updated.");
    }

    public async Task<OperationResult> AddCategoryAsync(string name)
    {
        name = (name ?? "").Trim();
        var problem = await ValidateCategoryAsync(name, null);
        if (problem is not null) return Fail(problem);

        _db.Categories.Add(new Category { Name = name });
        return await SaveAsync($"Category '{name}' created.");
    }

    public async Task<OperationResult> RenameCategoryAsync(int id, string name)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category is null) return Fail("Category not found.");
        name = (name ?? "").Trim();
        var problem = await ValidateCategoryAsync(name, id);
        if (problem is not null) return Fail(problem);

        category.Name = name;
        return await SaveAsync($"Category renamed to '{name}'.");
    }

    public async Task<OperationResult> DeleteCategoryAsync(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category is null) return Fail("Category not found.");
        if (await _db.MenuItems.AnyAsync(item => item.CategoryId == id))
            return Fail("This category contains menu items. Reassign those dishes before deleting it.");

        _db.Categories.Remove(category);
        return await SaveAsync($"Category '{category.Name}' deleted.");
    }

    private async Task<string?> ValidateMenuItemAsync(MenuItemChange change)
    {
        var name = (change.Name ?? "").Trim();
        if (name.Length is < 1 or > 120)
            return "Dish name must be between 1 and 120 characters.";
        if (change.Price is < 0.01m or > 99999.99m || decimal.Round(change.Price, 2) != change.Price)
            return "Price must be between RM 0.01 and RM 99,999.99 with no more than two decimal places.";
        if (!await _db.Categories.AnyAsync(c => c.Id == change.CategoryId))
            return "Select an existing category.";

        var tag = change.DietaryTag?.Trim();
        if (!string.IsNullOrEmpty(tag) && !AllowedDietaryTags.Contains(tag))
            return "Select a valid dietary tag.";

        if ((change.Description ?? "").Length > 2000)
            return "Description must be 2,000 characters or fewer.";

        var url = (change.ImageUrl ?? "").Trim();
        if (url.Length > 2048)
            return "Image URL must be 2,048 characters or fewer.";
        if (url.Length > 0 && (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)))
            return "Image URL must start with https:// or http://.";

        return null;
    }

    private async Task<string?> ValidateCategoryAsync(string name, int? currentId)
    {
        if (name.Length is < 1 or > 80)
            return "Category name must be between 1 and 80 characters.";

        var normalized = name.ToUpperInvariant();
        if (await _db.Categories.AnyAsync(c => c.Name.ToUpper() == normalized &&
            (!currentId.HasValue || c.Id != currentId.Value)))
            return "A category with that name already exists.";
        return null;
    }

    private static void ApplyChange(MenuItem item, MenuItemChange change)
    {
        item.Name = change.Name.Trim();
        item.Price = change.Price;
        item.CategoryId = change.CategoryId;
        item.DietaryTag = string.IsNullOrWhiteSpace(change.DietaryTag) ? null : change.DietaryTag.Trim();
        item.ImageUrl = (change.ImageUrl ?? "").Trim();
        item.Description = (change.Description ?? "").Trim();
    }

    private async Task<OperationResult> SaveAsync(string message)
    {
        try
        {
            await _db.SaveChangesAsync();
            return new OperationResult(true, message);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Menu was changed by another request");
            return Fail("This item was changed by another request. Refresh the page and try again.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Menu database operation failed");
            return Fail("The database could not save that change. Check that the record is not used elsewhere.");
        }
    }

    private static OperationResult Fail(string message) => new(false, message);
}
