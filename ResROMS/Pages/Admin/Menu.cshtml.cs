using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ResROMS.Pages.Admin
{
    public class MenuModel : PageModel
    {
        public class CategoryDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int ItemCount { get; set; }
        }

        public class MenuItemDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int CategoryId { get; set; }
            public string CategoryName { get; set; } = string.Empty;
            public bool IsAvailable { get; set; } = true;
            public string ImageUrl { get; set; } = string.Empty;
            public string DietaryTag { get; set; } = string.Empty; // Vegetarian, Vegan, Gluten-Free, Spicy
        }

        public List<CategoryDto> Categories { get; set; } = new();
        public List<MenuItemDto> MenuItems { get; set; } = new();

        [BindProperty]
        public MenuItemDto NewMenuItem { get; set; } = new();

        [BindProperty]
        public CategoryDto NewCategory { get; set; } = new();

        public void OnGet()
        {
            // Sample Categories
            Categories = new List<CategoryDto>
            {
                new CategoryDto { Id = 1, Name = "Appetizers", ItemCount = 3 },
                new CategoryDto { Id = 2, Name = "Main Courses", ItemCount = 4 },
                new CategoryDto { Id = 3, Name = "Desserts", ItemCount = 2 },
                new CategoryDto { Id = 4, Name = "Beverages", ItemCount = 3 }
            };

            // Sample Menu Items
            MenuItems = new List<MenuItemDto>
            {
                new MenuItemDto {
                    Id = 1,
                    Name = "Truffle Parmesan Fries",
                    Description = "Hand-cut fries tossed in white truffle oil, grated parmesan, and fresh parsley.",
                    Price = 9.50m,
                    CategoryId = 1,
                    CategoryName = "Appetizers",
                    IsAvailable = true,
                    ImageUrl = "https://images.unsplash.com/photo-1576107232684-1279f3908594?w=500&auto=format&fit=crop&q=60",
                    DietaryTag = "Vegetarian"
                },
                new MenuItemDto {
                    Id = 2,
                    Name = "Charbroiled Ribeye Steak",
                    Description = "12oz grass-fed ribeye served with garlic herb butter and roasted asparagus.",
                    Price = 34.00m,
                    CategoryId = 2,
                    CategoryName = "Main Courses",
                    IsAvailable = true,
                    ImageUrl = "https://images.unsplash.com/photo-1558030006-450675393462?w=500&auto=format&fit=crop&q=60",
                    DietaryTag = ""
                },
                new MenuItemDto {
                    Id = 3,
                    Name = "Wild Mushroom Risotto",
                    Description = "Arborio rice simmered with porcini mushrooms, thyme, and aged parmesan.",
                    Price = 21.50m,
                    CategoryId = 2,
                    CategoryName = "Main Courses",
                    IsAvailable = false,
                    ImageUrl = "https://images.unsplash.com/photo-1633964913295-ceb43826e7c9?w=500&auto=format&fit=crop&q=60",
                    DietaryTag = "Vegetarian"
                },
                new MenuItemDto {
                    Id = 4,
                    Name = "Molten Chocolate Lava Cake",
                    Description = "Warm dark chocolate cake with a molten center, served with vanilla bean ice cream.",
                    Price = 11.00m,
                    CategoryId = 3,
                    CategoryName = "Desserts",
                    IsAvailable = true,
                    ImageUrl = "https://images.unsplash.com/photo-1606313564200-e75d5e30476c?w=500&auto=format&fit=crop&q=60",
                    DietaryTag = "Vegetarian"
                },
                new MenuItemDto {
                    Id = 5,
                    Name = "Artisanal Smoked Old Fashioned",
                    Description = "Bourbon, angostura bitters, orange peel, smoked with oak wood chips.",
                    Price = 14.50m,
                    CategoryId = 4,
                    CategoryName = "Beverages",
                    IsAvailable = true,
                    ImageUrl = "https://images.unsplash.com/photo-1514362545857-3bc16c4c7d1b?w=500&auto=format&fit=crop&q=60",
                    DietaryTag = ""
                }
            };
        }

        public IActionResult OnPostAddMenuItem()
        {
            TempData["SuccessMessage"] = $"Dish '{NewMenuItem.Name}' added to menu!";
            return RedirectToPage();
        }

        public IActionResult OnPostAddCategory()
        {
            TempData["SuccessMessage"] = $"Category '{NewCategory.Name}' created!";
            return RedirectToPage();
        }
    }
}