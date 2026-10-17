namespace ResROMS.Services;

public sealed record DiningAreaSummary(int Id, string Name, int TableCount);
public sealed record TableSummary(int Id, string TableNumber, int Capacity, int DiningAreaId, string Status, string Shape);
public sealed record OperationResult(bool Success, string Message);

public interface ITableManagementService
{
    Task<IReadOnlyList<DiningAreaSummary>> GetAreasAsync();
    Task<IReadOnlyList<TableSummary>> GetTablesAsync();
    Task<OperationResult> AddAreaAsync(string name);
    Task<OperationResult> RenameAreaAsync(int id, string name);
    Task<OperationResult> DeleteAreaAsync(int id);
    Task<OperationResult> AddTableAsync(string tableNumber, int capacity, int diningAreaId, string shape);
    Task<OperationResult> EditTableAsync(int id, string tableNumber, int capacity, int diningAreaId, string shape);
    Task<OperationResult> ChangeStatusAsync(int id, string status);
    Task<OperationResult> DeleteTableAsync(int id);
}
