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
        public DbSet<RestaurantProfile> RestaurantProfiles { get; set; }
        public DbSet<OperatingHour> OperatingHours { get; set; }
        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Payment> Payments { get; set; }

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

            // Singleton profile: the app currently manages ONE restaurant.
            modelBuilder.Entity<RestaurantProfile>(profile =>
            {
                profile.HasKey(p => p.Id);
                profile.Property(p => p.Id).ValueGeneratedNever();
                profile.ToTable(table => table.HasCheckConstraint(
                    "CK_RestaurantProfiles_Singleton", "[Id] = 1"));
            });

            modelBuilder.Entity<OperatingHour>(hours =>
            {
                // Seven fixed rows; no generated surrogate key required.
                hours.HasKey(h => h.DayOfWeek);
                hours.Property(h => h.DayOfWeek).ValueGeneratedNever();
                hours.ToTable(table =>
                {
                    table.HasCheckConstraint("CK_OperatingHours_DayOfWeek",
                        "[DayOfWeek] BETWEEN 0 AND 6");
                    table.HasCheckConstraint("CK_OperatingHours_OpeningWindow",
                        "[IsClosed] = 1 OR [OpeningTime] <> [ClosingTime]");
                });
            });

            // Campaigns have optional, unique case-insensitive promo codes and
            // bounded calendar dates in the restaurant's local time zone.
            modelBuilder.Entity<Campaign>(campaign =>
            {
                campaign.Property(c => c.Name).HasMaxLength(120);
                campaign.Property(c => c.PromoCode).HasMaxLength(24);
                campaign.Property(c => c.Type).HasMaxLength(20);
                campaign.Property(c => c.DiscountType).HasMaxLength(20);
                campaign.Property(c => c.Description).HasMaxLength(1000);
                campaign.Property(c => c.DiscountValue).HasPrecision(10, 2);
                campaign.Property(c => c.StartDate).HasColumnType("date");
                campaign.Property(c => c.EndDate).HasColumnType("date");
                campaign.HasIndex(c => c.PromoCode).IsUnique()
                    .HasFilter("[PromoCode] IS NOT NULL");
                campaign.ToTable(table =>
                {
                    table.HasCheckConstraint("CK_Campaigns_DateRange", "[EndDate] >= [StartDate]");
                    table.HasCheckConstraint("CK_Campaigns_UsageCount", "[UsageCount] >= 0");
                    table.HasCheckConstraint("CK_Campaigns_Type",
                        "[Type] IN ('Discount', 'Happy Hour', 'Seasonal')");
                    table.HasCheckConstraint("CK_Campaigns_DiscountType",
                        "[DiscountType] IN ('None', 'Percentage', 'FixedAmount')");
                    table.HasCheckConstraint("CK_Campaigns_DiscountValue",
                        "([DiscountType] = 'None' AND [DiscountValue] = 0) OR " +
                        "([DiscountType] = 'Percentage' AND [DiscountValue] > 0 AND [DiscountValue] <= 100) OR " +
                        "([DiscountType] = 'FixedAmount' AND [DiscountValue] > 0)");
                });
            });

            // One order contains many immutable-priced line items; delete order items
            // only if the parent order itself is explicitly removed (no Admin delete UI).
            modelBuilder.Entity<Order>(order =>
            {
                order.Property(o => o.Subtotal).HasPrecision(12, 2);
                order.Property(o => o.DiscountAmount).HasPrecision(12, 2);
                order.Property(o => o.TotalAmount).HasPrecision(12, 2);
                order.HasIndex(o => new { o.Status, o.OrderedUtc });
                order.HasIndex(o => new { o.CustomerId, o.OrderedUtc });
                order.HasOne(o => o.Customer).WithMany()
                    .HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
                order.HasOne(o => o.Table).WithMany()
                    .HasForeignKey(o => o.TableId).OnDelete(DeleteBehavior.Restrict);
                order.HasOne(o => o.Reservation).WithMany()
                    .HasForeignKey(o => o.ReservationId).OnDelete(DeleteBehavior.Restrict);
                order.HasOne(o => o.CreatedByUser).WithMany()
                    .HasForeignKey(o => o.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
                order.ToTable(table =>
                {
                    table.HasCheckConstraint("CK_Orders_Status",
                        "[Status] IN ('New','Cooking','Ready','Completed','Cancelled')");
                    table.HasCheckConstraint("CK_Orders_Amounts",
                        "[Subtotal] >= 0 AND [DiscountAmount] >= 0 AND " +
                        "[DiscountAmount] <= [Subtotal] AND [TotalAmount] = [Subtotal] - [DiscountAmount]");
                });
            });
            modelBuilder.Entity<OrderItem>(item =>
            {
                item.Property(i => i.UnitPrice).HasPrecision(12, 2);
                item.Property(i => i.LineTotal).HasPrecision(12, 2);
                item.HasOne(i => i.Order).WithMany(o => o.Items)
                    .HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
                item.HasOne(i => i.MenuItem).WithMany()
                    .HasForeignKey(i => i.MenuItemId).OnDelete(DeleteBehavior.Restrict);
                item.ToTable(table =>
                {
                    table.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] BETWEEN 1 AND 99");
                    table.HasCheckConstraint("CK_OrderItems_Amounts",
                        "[UnitPrice] >= 0 AND [LineTotal] = [UnitPrice] * [Quantity]");
                });
            });
            modelBuilder.Entity<Payment>(payment =>
            {
                payment.Property(p => p.Amount).HasPrecision(12, 2);
                payment.HasOne(p => p.Order).WithOne(o => o.Payment)
                    .HasForeignKey<Payment>(p => p.OrderId).OnDelete(DeleteBehavior.Restrict);
                payment.HasOne(p => p.RecordedByUser).WithMany()
                    .HasForeignKey(p => p.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
                payment.HasIndex(p => p.RecordedUtc);
                payment.ToTable(table =>
                {
                    table.HasCheckConstraint("CK_Payments_Method",
                        "[Method] IN ('Cash','Card','BankTransfer')");
                    table.HasCheckConstraint("CK_Payments_Status",
                        "[Status] IN ('Paid','Refunded')");
                    table.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
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