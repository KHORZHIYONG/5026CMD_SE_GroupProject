using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;

namespace ResROMS.Services;

public sealed class TableManagementService : ITableManagementService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TableManagementService> _logger;

    public TableManagementService(AppDbContext db, ILogger<TableManagementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiningAreaSummary>> GetAreasAsync() =>
        await _db.DiningAreas.AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new DiningAreaSummary(a.Id, a.Name, a.Tables.Count))
            .ToListAsync();

    public async Task<IReadOnlyList<TableSummary>> GetTablesAsync() =>
        await _db.Tables.AsNoTracking()
            .OrderBy(t => t.TableNumber)
            .Select(t => new TableSummary(t.Id, t.TableNumber, t.Capacity,
                t.DiningAreaId, t.Status, t.Shape))
            .ToListAsync();

    public async Task<OperationResult> AddAreaAsync(string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 80)
            return Error("Area name must contain 1–80 characters.");
        if (await _db.DiningAreas.AnyAsync(a => a.Name == name))
            return Error("A dining area with that name already exists.");

        _db.DiningAreas.Add(new DiningArea { Name = name });
        return await SaveAsync($"Dining area '{name}' created.");
    }

    public async Task<OperationResult> RenameAreaAsync(int id, string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 80)
            return Error("Area name must contain 1–80 characters.");
        var area = await _db.DiningAreas.FindAsync(id);
        if (area is null) return Error("Dining area not found.");
        if (await _db.DiningAreas.AnyAsync(a => a.Id != id && a.Name == name))
            return Error("A dining area with that name already exists.");
        area.Name = name;
        return await SaveAsync($"Dining area renamed to '{name}'.");
    }

    public async Task<OperationResult> DeleteAreaAsync(int id)
    {
        var area = await _db.DiningAreas.FindAsync(id);
        if (area is null) return Error("Dining area not found.");
        if (await _db.Tables.AnyAsync(t => t.DiningAreaId == id))
            return Error("Move or delete the tables in this area before deleting it.");
        _db.DiningAreas.Remove(area);
        return await SaveAsync($"Dining area '{area.Name}' deleted.");
    }

    public async Task<OperationResult> AddTableAsync(string tableNumber, int capacity, int diningAreaId, string shape)
    {
        tableNumber = tableNumber.Trim();
        var problem = await ValidateTableAsync(tableNumber, capacity, diningAreaId, shape, null);
        if (problem is not null) return Error(problem);

        _db.Tables.Add(new Table {
            TableNumber = tableNumber, Capacity = capacity, DiningAreaId = diningAreaId,
            Shape = shape, Status = "Available"
        });
        return await SaveAsync($"Table '{tableNumber}' created.");
    }

    public async Task<OperationResult> EditTableAsync(int id, string tableNumber, int capacity, int diningAreaId, string shape)
    {
        tableNumber = tableNumber.Trim();
        var table = await _db.Tables.FindAsync(id);
        if (table is null) return Error("Table not found.");
        var problem = await ValidateTableAsync(tableNumber, capacity, diningAreaId, shape, id);
        if (problem is not null) return Error(problem);

        // Do not reduce seats below the party size of an upcoming booking.
        if (await _db.Reservations.AnyAsync(r => r.TableId == id &&
            r.EndUtc > DateTime.UtcNow && (r.Status == "Pending" || r.Status == "Confirmed")
            && r.PartySize > capacity))
            return Error("A future reservation requires more seats than the new capacity.");

        table.TableNumber = tableNumber;
        table.Capacity = capacity;
        table.DiningAreaId = diningAreaId;
        table.Shape = shape;
        return await SaveAsync($"Table '{tableNumber}' updated.");
    }

    public async Task<OperationResult> ChangeStatusAsync(int id, string status)
    {
        if (status is not ("Available" or "Occupied" or "Reserved" or "Maintenance"))
            return Error("Invalid table status.");
        var table = await _db.Tables.FindAsync(id);
        if (table is null) return Error("Table not found.");
        if (status == "Maintenance" && await _db.Reservations.AnyAsync(r =>
            r.TableId == id && r.EndUtc > DateTime.UtcNow &&
            (r.Status == "Pending" || r.Status == "Confirmed")))
            return Error("Cancel or reassign upcoming reservations before setting Maintenance.");
        table.Status = status;
        return await SaveAsync($"Table '{table.TableNumber}' is now {status}.");
    }

    public async Task<OperationResult> DeleteTableAsync(int id)
    {
        var table = await _db.Tables.FindAsync(id);
        if (table is null) return Error("Table not found.");
        // Preserve past reservation history rather than silently deleting it.
        if (await _db.Reservations.AnyAsync(r => r.TableId == id))
            return Error("This table has reservation history and cannot be deleted. Mark it Maintenance instead.");
        _db.Tables.Remove(table);
        return await SaveAsync($"Table '{table.TableNumber}' deleted.");
    }

    private async Task<string?> ValidateTableAsync(
        string tableNumber, int capacity, int diningAreaId, string shape, int? currentId)
    {
        if (tableNumber.Length is < 1 or > 20)
            return "Table number must contain 1–20 characters.";
        if (capacity is < 1 or > 20)
            return "Table capacity must be between 1 and 20.";
        if (shape is not ("Square" or "Round" or "Rectangle"))
            return "Invalid table shape.";
        if (!await _db.DiningAreas.AnyAsync(a => a.Id == diningAreaId))
            return "Select an existing dining area.";
        if (await _db.Tables.AnyAsync(t => t.TableNumber == tableNumber &&
                    (!currentId.HasValue || t.Id != currentId.Value)))
            return "A table with that number already exists.";
        return null;
    }

    private async Task<OperationResult> SaveAsync(string successMessage)
    {
        try
        {
            await _db.SaveChangesAsync();
            return new OperationResult(true, successMessage);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Failed to save restaurant table/area data");
            return Error("Database rejected this change. Check for duplicates or related records.");
        }
    }

    private static OperationResult Error(string message) => new(false, message);
}
