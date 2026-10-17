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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DiningArea>()
                .HasIndex(area => area.Name)
                .IsUnique();

            modelBuilder.Entity<Table>()
                .HasIndex(table => table.TableNumber)
                .IsUnique();

            // Existing tables should receive Square when the new column is added.
            modelBuilder.Entity<Table>()
                .Property(table => table.Shape)
                .HasDefaultValue("Square");

            // Do not cascade-delete tables when a dining area is removed.
            modelBuilder.Entity<Table>()
                .HasOne(table => table.DiningArea)
                .WithMany(area => area.Tables)
                .HasForeignKey(table => table.DiningAreaId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}