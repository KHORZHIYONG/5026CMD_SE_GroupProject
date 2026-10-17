using ResROMS.Models;

namespace ResROMS.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            // Manage schema changes using EF Core migrations (Update-Database).
            // Do not call EnsureCreated() on a migrations-managed database.

            // Check if any Menu Items already exist. If yes, stop running the seeder.
            if (context.MenuItems.Any())
            {
                return;
            }

            // 1. Seed Categories
            var mains = new Category { Name = "Main Course" };
            var starters = new Category { Name = "Starters" };
            var drinks = new Category { Name = "Beverages" };

            context.Categories.AddRange(mains, starters, drinks);

            // 2. Seed Menu Items
            context.MenuItems.AddRange(
                new MenuItem { Name = "Ribeye Steak", Description = "250g grilled beef steak with garlic butter", Price = 38.50m, Category = mains, IsAvailable = true },
                new MenuItem { Name = "Truffle Fries", Description = "Hand-cut fries tossed in truffle oil", Price = 12.00m, Category = starters, IsAvailable = true },
                new MenuItem { Name = "Iced Lemon Tea", Description = "Freshly brewed tea with lemon", Price = 4.50m, Category = drinks, IsAvailable = true }
            );

            // 3. Seed Staff/Users
            context.Users.AddRange(
                new User { FullName = "Alex Rivera", Email = "alex@resroms.com", Phone = "555-0192", Role = "Manager", IsActive = true },
                new User { FullName = "Sarah Jenkins", Email = "sarah@resroms.com", Phone = "555-0144", Role = "Waiter", IsActive = true }
            );

            // Save everything to LocalDB!
            context.SaveChanges();
        }
    }
}