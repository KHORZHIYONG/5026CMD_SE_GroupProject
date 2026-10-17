using ResROMS.Models;

namespace ResROMS.Services;

// Form data deliberately excludes EF navigation properties and availability flags.
public sealed record MenuItemChange(
    string Name, decimal Price, int CategoryId, string? DietaryTag,
    string? ImageUrl, string? Description);

public sealed record MenuCatalog(
    IReadOnlyList<Category> Categories, IReadOnlyList<MenuItem> MenuItems);

public interface IMenuManagementService
{
    Task<MenuCatalog> GetCatalogAsync();
    Task<OperationResult> AddMenuItemAsync(MenuItemChange change);
    Task<OperationResult> EditMenuItemAsync(int id, MenuItemChange change);
    Task<OperationResult> DeleteMenuItemAsync(int id);
    Task<OperationResult> SetAvailabilityAsync(int id, bool isAvailable);
    Task<OperationResult> AddCategoryAsync(string name);
    Task<OperationResult> RenameCategoryAsync(int id, string name);
    Task<OperationResult> DeleteCategoryAsync(int id);
}
