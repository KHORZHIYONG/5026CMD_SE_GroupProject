using Microsoft.EntityFrameworkCore;
using ResROMS.Models;

namespace ResROMS.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<DiningArea> DiningAreas { get; set; }
        public DbSet<Table> Tables { get; set; }
    }
}