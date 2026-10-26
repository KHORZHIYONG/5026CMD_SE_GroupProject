using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;

namespace ResROMS.Services;

public sealed class OrderManagementService : IOrderManagementService
{
    private readonly AppDbContext _db;
    private readonly ILogger<OrderManagementService> _logger;

    public OrderManagementService(AppDbContext db, ILogger<OrderManagementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OrderSummary>> GetOrdersAsync()
    {
        var rows = await _db.Orders.AsNoTracking()
            .Include(o => o.Items).Include(o => o.Table).Include(o => o.Customer)
            .Include(o => o.CreatedByUser).Include(o => o.Payment)
            .OrderByDescending(o => o.OrderedUtc).ThenByDescending(o => o.Id)
            .Take(200).AsSplitQuery().ToListAsync();
        return rows.Select(o => new OrderSummary(o.Id,
            o.Table?.TableNumber ?? "Takeaway / no table",
            o.Customer?.FullName ?? "Walk-in", o.CreatedByUser?.FullName ?? "System",
            o.Status, ReservationClock.ToLocal(o.OrderedUtc), o.Subtotal, o.TotalAmount,
            o.Payment?.Status ?? "Unpaid",
            o.Items.OrderBy(i => i.Id).Select(i => new OrderLineSummary(
                i.ItemName, i.Quantity, i.UnitPrice, i.LineTotal)).ToArray())).ToArray();
    }

    public async Task<IReadOnlyList<PaymentSummary>> GetPaymentsAsync()
    {
        var rows = await _db.Payments.AsNoTracking().Include(p => p.RecordedByUser)
            .OrderByDescending(p => p.RecordedUtc).ThenByDescending(p => p.Id)
            .Take(200).ToListAsync();
        return rows.Select(p => new PaymentSummary(p.Id, p.OrderId, p.Amount,
            p.Method, p.Status, p.Reference, ReservationClock.ToLocal(p.RecordedUtc),
            p.RefundedUtc.HasValue ? ReservationClock.ToLocal(p.RefundedUtc.Value) : null,
            p.RecordedByUser?.FullName ?? "System")).ToArray();
    }

    public async Task<IReadOnlyList<MenuChoice>> GetMenuChoicesAsync() =>
        await _db.MenuItems.AsNoTracking().Where(i => i.IsAvailable)
            .OrderBy(i => i.Category!.Name).ThenBy(i => i.Name)
            .Select(i => new MenuChoice(i.Id, i.Name, i.Price, i.Category!.Name))
            .ToListAsync();

    public async Task<IReadOnlyList<AccountChoice>> GetCustomerChoicesAsync() =>
        await _db.Users.AsNoTracking().Where(u => u.Role == "Customer" && u.IsActive)
            .OrderBy(u => u.FullName).Select(u => new AccountChoice(u.Id, u.FullName))
            .ToListAsync();

    public async Task<IReadOnlyList<TableChoice>> GetTableChoicesAsync() =>
        await _db.Tables.AsNoTracking().Where(t => t.Status != "Maintenance")
            .OrderBy(t => t.TableNumber)
            .Select(t => new TableChoice(t.Id, t.TableNumber)).ToListAsync();

    public async Task<IReadOnlyList<OrderOption>> GetUnpaidOrdersAsync()
    {
        var rows = await _db.Orders.AsNoTracking()
            .Where(o => o.Status != "Cancelled" && o.Payment == null)
            .OrderByDescending(o => o.OrderedUtc).Take(100)
            .Select(o => new { o.Id, o.TotalAmount }).ToListAsync();
        return rows.Select(o => new OrderOption(o.Id,
            $"Order #{o.Id} — RM {o.TotalAmount:F2}")).ToArray();
    }

    public async Task<OrderActionResult> CreateAsync(CreateOrderInput input)
    {
        if (input.Lines is null || input.Lines.Count is < 1 or > 20)
            return Fail("An order must contain 1–20 menu lines.");
        if (input.Lines.Any(l => l.MenuItemId <= 0 || l.Quantity is < 1 or > 99))
            return Fail("Choose valid menu items and quantities between 1 and 99.");
        if (input.Lines.GroupBy(l => l.MenuItemId).Any(g => g.Count() != 1))
            return Fail("Choose each dish only once. Increase its quantity instead.");

        if (input.CreatedByUserId.HasValue && !await _db.Users.AnyAsync(u =>
            u.Id == input.CreatedByUserId.Value && u.IsActive))
            return Fail("Current account is no longer active. Sign in again.");

        if (input.CustomerId.HasValue && !await _db.Users.AnyAsync(u =>
            u.Id == input.CustomerId.Value && u.Role == "Customer" && u.IsActive))
            return Fail("Select a valid active Customer account or choose Walk-in.");

        if (input.TableId.HasValue && !await _db.Tables.AnyAsync(t =>
            t.Id == input.TableId.Value && t.Status != "Maintenance"))
            return Fail("Choose an existing table not under maintenance.");

        if (input.ReservationId.HasValue)
        {
            var reservation = await _db.Reservations.AsNoTracking()
                .SingleOrDefaultAsync(r => r.Id == input.ReservationId.Value);
            if (reservation is null || reservation.Status is not ("Confirmed" or "Completed"))
                return Fail("Only confirmed or completed reservations may be linked to an order.");
            if (input.CustomerId != reservation.CustomerId || input.TableId != reservation.TableId)
                return Fail("Reservation customer and table must match the order.");
        }

        var ids = input.Lines.Select(l => l.MenuItemId).ToArray();
        var products = await _db.MenuItems.AsNoTracking()
            .Where(i => ids.Contains(i.Id) && i.IsAvailable).ToListAsync();
        if (products.Count != ids.Length)
            return Fail("One or more dishes are no longer available.");
        if (products.Any(p => p.Price < 0 || decimal.Round(p.Price, 2) != p.Price))
            return Fail("A menu item has an invalid price. Correct its price first.");

        var lookup = products.ToDictionary(p => p.Id);
        var now = DateTime.UtcNow;
        var order = new Order
        {
            CustomerId = input.CustomerId, TableId = input.TableId,
            ReservationId = input.ReservationId, CreatedByUserId = input.CreatedByUserId,
            OrderedUtc = now, UpdatedUtc = now, Status = "New"
        };
        foreach (var line in input.Lines)
        {
            var product = lookup[line.MenuItemId];
            order.Items.Add(new OrderItem
            {
                MenuItemId = product.Id, ItemName = product.Name.Length > 120
                    ? product.Name[..120] : product.Name,
                Quantity = line.Quantity, UnitPrice = product.Price,
                LineTotal = product.Price * line.Quantity
            });
        }
        try
        {
            order.Subtotal = order.Items.Sum(i => i.LineTotal);
            order.TotalAmount = order.Subtotal;
            if (order.TotalAmount > 9999999999.99m)
                return Fail("Order total exceeds the supported amount.");
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
            return Ok($"Order #{order.Id} created and stored in SQL Server.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Failed to create order");
            return Fail("Could not save the order. Check menu, table and customer selections.");
        }
    }

    public async Task<OrderActionResult> UpdateStatusAsync(int orderId, string newStatus)
    {
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await _db.Orders.FromSqlInterpolated(
                $"SELECT * FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK) WHERE Id = {orderId}")
                .SingleOrDefaultAsync();
            if (order is null) return Fail("Order not found.");
            var allowed = (order.Status, newStatus) switch
            {
                ("New", "Cooking") => true,
                ("Cooking", "Ready") => true,
                ("Ready", "Completed") => true,
                ("New", "Cancelled") => true,
                ("Cooking", "Cancelled") => true,
                _ => false
            };
            if (!allowed) return Fail("Order status has changed. Refresh the page and retry.");
            if (newStatus == "Cancelled" && await _db.Payments.AnyAsync(p =>
                p.OrderId == orderId && p.Status == "Paid"))
                return Fail("Refund the recorded payment before cancelling this order.");
            order.Status = newStatus;
            order.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok($"Order #{orderId} updated to {newStatus}.");
        }
        catch (Exception ex) when (ex is DbUpdateException || ex is SqlException)
        {
            _logger.LogWarning(ex, "Could not update order status");
            return Fail("The order changed or could not be updated. Please refresh.");
        }
    }

    public async Task<OrderActionResult> RecordPaymentAsync(
        int orderId, string method, string? reference, int? adminUserId)
    {
        if (method is not ("Cash" or "Card" or "BankTransfer"))
            return Fail("Choose Cash, Card or Bank Transfer.");
        reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (reference?.Length > 100) return Fail("Payment reference must be at most 100 characters.");
        if (adminUserId.HasValue && !await _db.Users.AnyAsync(u =>
            u.Id == adminUserId && u.IsActive && u.Role == "Admin"))
            return Fail("Administrator account could not be verified.");
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await _db.Orders.FromSqlInterpolated(
                $"SELECT * FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK) WHERE Id = {orderId}")
                .SingleOrDefaultAsync();
            if (order is null) return Fail("Order not found.");
            if (order.Status == "Cancelled") return Fail("Cannot record payment on a cancelled order.");
            if (await _db.Payments.AnyAsync(p => p.OrderId == orderId))
                return Fail("This order already has a payment record.");
            _db.Payments.Add(new Payment
            {
                OrderId = orderId, Amount = order.TotalAmount, Method = method,
                Status = "Paid", Reference = reference, RecordedByUserId = adminUserId,
                RecordedUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok($"Manual payment record saved for order #{orderId}.");
        }
        catch (Exception ex) when (ex is DbUpdateException || ex is SqlException)
        {
            _logger.LogWarning(ex, "Failed to save manual payment");
            return Fail("Payment could not be saved. Refresh and check for a duplicate.");
        }
    }

    public async Task<OrderActionResult> MarkRefundedAsync(int paymentId)
    {
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var payment = await _db.Payments.FromSqlInterpolated(
                $"SELECT * FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK) WHERE Id = {paymentId}")
                .SingleOrDefaultAsync();
            if (payment is null || payment.Status != "Paid")
                return Fail("Only a paid transaction can be marked refunded.");
            // Bookkeeping only: the actual refund must have been made externally.
            payment.Status = "Refunded";
            payment.RefundedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok($"Payment #{paymentId} marked as refunded (record only).");
        }
        catch (Exception ex) when (ex is DbUpdateException || ex is SqlException)
        {
            _logger.LogWarning(ex, "Failed to update refund record");
            return Fail("Unable to update refund record. Refresh and retry.");
        }
    }

    private static OrderActionResult Ok(string message) => new(true, message);
    private static OrderActionResult Fail(string message) => new(false, message);
}
