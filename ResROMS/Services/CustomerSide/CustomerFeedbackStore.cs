using Microsoft.EntityFrameworkCore;
using ResROMS.Data;

namespace ResROMS.Services.CustomerSide;

public sealed class FeedbackRow
{
    public int Id { get; set; }
    public int? OrderId { get; set; }
    public int? ReservationId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Stores customer ratings in its own table (dbo.CustomerFeedback), created on first use.
/// This keeps the customer feature independent of the EF migration history owned by the
/// rest of the team: no change to AppDbContext or Migrations is needed.
/// </summary>
public static class CustomerFeedbackStore
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _ready;

    private const string CreateTableSql = @"
IF OBJECT_ID(N'dbo.CustomerFeedback', N'U') IS NULL
CREATE TABLE dbo.CustomerFeedback (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerFeedback PRIMARY KEY,
    CustomerId INT NOT NULL,
    OrderId INT NULL,
    ReservationId INT NULL,
    Rating INT NOT NULL,
    Comment NVARCHAR(500) NOT NULL,
    CreatedUtc DATETIME2 NOT NULL,
    CONSTRAINT FK_CustomerFeedback_Users FOREIGN KEY (CustomerId) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_CustomerFeedback_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id),
    CONSTRAINT FK_CustomerFeedback_Reservations FOREIGN KEY (ReservationId) REFERENCES dbo.Reservations(Id),
    CONSTRAINT CK_CustomerFeedback_Rating CHECK (Rating BETWEEN 1 AND 5),
    CONSTRAINT CK_CustomerFeedback_Target CHECK (
        (OrderId IS NOT NULL AND ReservationId IS NULL) OR (OrderId IS NULL AND ReservationId IS NOT NULL))
)";

    private static async Task EnsureAsync(AppDbContext db, CancellationToken ct)
    {
        if (_ready) return;
        await Gate.WaitAsync(ct);
        try
        {
            if (_ready) return;
            await db.Database.ExecuteSqlRawAsync(CreateTableSql, ct);
            _ready = true;
        }
        finally { Gate.Release(); }
    }

    public static async Task<List<FeedbackRow>> GetForCustomerAsync(AppDbContext db, int customerId, CancellationToken ct = default)
    {
        await EnsureAsync(db, ct);
        return await db.Database.SqlQuery<FeedbackRow>(
            $"SELECT Id, OrderId, ReservationId, Rating, Comment, CreatedUtc FROM dbo.CustomerFeedback WHERE CustomerId = {customerId} ORDER BY CreatedUtc DESC")
            .ToListAsync(ct);
    }

    public static async Task<List<int>> RatedOrderIdsAsync(AppDbContext db, int customerId, CancellationToken ct = default)
    {
        await EnsureAsync(db, ct);
        return await db.Database.SqlQuery<int>(
            $"SELECT OrderId AS Value FROM dbo.CustomerFeedback WHERE CustomerId = {customerId} AND OrderId IS NOT NULL")
            .ToListAsync(ct);
    }

    public static async Task<List<int>> RatedReservationIdsAsync(AppDbContext db, int customerId, CancellationToken ct = default)
    {
        await EnsureAsync(db, ct);
        return await db.Database.SqlQuery<int>(
            $"SELECT ReservationId AS Value FROM dbo.CustomerFeedback WHERE CustomerId = {customerId} AND ReservationId IS NOT NULL")
            .ToListAsync(ct);
    }

    /// <summary>Returns false if this order was already rated.</summary>
    public static async Task<bool> AddForOrderAsync(AppDbContext db, int customerId, int orderId, int rating, string comment, CancellationToken ct = default)
    {
        await EnsureAsync(db, ct);
        var now = DateTime.UtcNow;
        var rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO dbo.CustomerFeedback (CustomerId, OrderId, Rating, Comment, CreatedUtc)
SELECT {customerId}, {orderId}, {rating}, {comment}, {now}
WHERE NOT EXISTS (SELECT 1 FROM dbo.CustomerFeedback WHERE CustomerId = {customerId} AND OrderId = {orderId})", ct);
        return rows > 0;
    }

    /// <summary>Returns false if this reservation was already rated.</summary>
    public static async Task<bool> AddForReservationAsync(AppDbContext db, int customerId, int reservationId, int rating, string comment, CancellationToken ct = default)
    {
        await EnsureAsync(db, ct);
        var now = DateTime.UtcNow;
        var rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO dbo.CustomerFeedback (CustomerId, ReservationId, Rating, Comment, CreatedUtc)
SELECT {customerId}, {reservationId}, {rating}, {comment}, {now}
WHERE NOT EXISTS (SELECT 1 FROM dbo.CustomerFeedback WHERE CustomerId = {customerId} AND ReservationId = {reservationId})", ct);
        return rows > 0;
    }
}
