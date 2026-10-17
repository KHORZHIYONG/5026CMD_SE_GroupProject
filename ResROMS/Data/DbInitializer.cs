using ResROMS.Models;

namespace ResROMS.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context)
    {
        // IMPORTANT: Schema is managed with EF Core migrations. Run Update-Database first.
        // Every seed is checked independently: an existing menu must not suppress new settings.
        if (!context.MenuItems.Any())
        {
            var mains = new Category { Name = "Main Course" };
            var starters = new Category { Name = "Starters" };
            var drinks = new Category { Name = "Beverages" };
            context.Categories.AddRange(mains, starters, drinks);
            context.MenuItems.AddRange(
                new MenuItem { Name = "Ribeye Steak", Description = "250g grilled beef steak with garlic butter", Price = 38.50m, Category = mains, IsAvailable = true },
                new MenuItem { Name = "Truffle Fries", Description = "Hand-cut fries tossed in truffle oil", Price = 12.00m, Category = starters, IsAvailable = true },
                new MenuItem { Name = "Iced Lemon Tea", Description = "Freshly brewed tea with lemon", Price = 4.50m, Category = drinks, IsAvailable = true }
            );
            // Only seed demonstration users for a brand-new database.
            if (!context.Users.Any())
                context.Users.AddRange(
                    new User { FullName = "Alex Rivera", Email = "alex@resroms.com", Phone = "555-0192", Role = "Manager", IsActive = true },
                    new User { FullName = "Sarah Jenkins", Email = "sarah@resroms.com", Phone = "555-0144", Role = "Waiter", IsActive = true }
                );
        }

        if (!context.RestaurantProfiles.Any())
        {
            context.RestaurantProfiles.Add(new RestaurantProfile
            {
                Id = 1,
                Name = "ResROMS Bistro",
                Phone = "+60 12-345 6789",
                Email = "hello@resromsbistro.com",
                Address = "123 Culinary Lane, Jelutong, Penang, Malaysia"
            });
        }

        // Starter hours are placeholders from the original Monday mock UI.
        // Administrator MUST review all seven days before treating bookings as live.
        var existingDays = context.OperatingHours.Select(h => h.DayOfWeek).ToHashSet();
        for (var day = 0; day < 7; day++)
        {
            if (!existingDays.Contains(day))
                context.OperatingHours.Add(new OperatingHour
                {
                    DayOfWeek = day, IsClosed = false,
                    OpeningTime = new TimeOnly(9, 0), ClosingTime = new TimeOnly(22, 0)
                });
        }
        context.SaveChanges();
    }
}
