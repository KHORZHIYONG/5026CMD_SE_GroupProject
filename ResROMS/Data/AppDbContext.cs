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
        public DbSet<Reservation> Reservations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // SQL Server cannot index nvarchar(max); make Email a bounded key column.
            modelBuilder.Entity<User>().Property(user => user.Email).HasMaxLength(254);
            // Email addresses must uniquely identify an account.
            modelBuilder.Entity<User>()
                .HasIndex(user => user.Email)
                .IsUnique();

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

            modelBuilder.Entity<Reservation>(reservation =>
            {
                reservation.Property(r => r.Status).HasMaxLength(20);
                reservation.Property(r => r.SpecialRequests).HasMaxLength(500);
                reservation.HasIndex(r => new { r.TableId, r.StartUtc, r.EndUtc });
                reservation.HasIndex(r => new { r.CustomerId, r.StartUtc });
                reservation.HasOne(r => r.Customer).WithMany()
                    .HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
                reservation.HasOne(r => r.Table).WithMany()
                    .HasForeignKey(r => r.TableId).OnDelete(DeleteBehavior.Restrict);
                reservation.ToTable(table =>
                {
                    table.HasCheckConstraint("CK_Reservations_PartySize", "[PartySize] BETWEEN 1 AND 20");
                    table.HasCheckConstraint("CK_Reservations_DateRange", "[EndUtc] > [StartUtc]");
                    table.HasCheckConstraint("CK_Reservations_Status", "[Status] IN ('Pending', 'Confirmed', 'Cancelled', 'Completed')");
                });
            });

            // Do not cascade-delete tables when a dining area is removed.
            modelBuilder.Entity<Table>()
                .HasOne(table => table.DiningArea)
                .WithMany(area => area.Tables)
                .HasForeignKey(table => table.DiningAreaId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}