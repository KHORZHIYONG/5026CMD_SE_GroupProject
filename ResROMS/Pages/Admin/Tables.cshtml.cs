using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResROMS.Services;

namespace ResROMS.Pages.Admin;

public class TablesModel : PageModel
{
    private readonly ITableManagementService _tables;

    public TablesModel(ITableManagementService tables) => _tables = tables;

    public class TableForm
    {
        [Required, StringLength(20)]
        public string TableNumber { get; set; } = string.Empty;

        [Range(1, 20)]
        public int Capacity { get; set; } = 4;

        [Range(1, int.MaxValue, ErrorMessage = "Choose a dining area.")]
        public int DiningAreaId { get; set; }

        [Required, RegularExpression("Square|Round|Rectangle")]
        public string Shape { get; set; } = "Square";
    }

    public class AreaForm
    {
        [Required, StringLength(80)]
        public string Name { get; set; } = string.Empty;
    }

    // These properties exist to support asp-for tags. Forms bind to handler parameters
    // so submitting NewArea does not accidentally validate NewTable (and vice versa).
    public TableForm NewTable { get; set; } = new();
    public AreaForm NewArea { get; set; } = new();

    public IReadOnlyList<DiningAreaSummary> DiningAreas { get; private set; } = [];
    public IReadOnlyList<TableSummary> Tables { get; private set; } = [];
    public string? ActiveModal { get; private set; }

    public async Task OnGetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        DiningAreas = await _tables.GetAreasAsync();
        Tables = await _tables.GetTablesAsync();
    }

    private IActionResult Result(OperationResult result)
    {
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddAreaAsync([Bind(Prefix = "NewArea")] AreaForm input)
    {
        if (!ModelState.IsValid)
        {
            ActiveModal = "addAreaModal";
            await LoadAsync();
            return Page();
        }
        return Result(await _tables.AddAreaAsync(input.Name));
    }

    public async Task<IActionResult> OnPostRenameAreaAsync(int id, [Bind(Prefix = "RenameArea")] AreaForm input)
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }
        return Result(await _tables.RenameAreaAsync(id, input.Name));
    }

    public async Task<IActionResult> OnPostDeleteAreaAsync(int id) =>
        Result(await _tables.DeleteAreaAsync(id));

    public async Task<IActionResult> OnPostAddTableAsync([Bind(Prefix = "NewTable")] TableForm input)
    {
        if (!ModelState.IsValid)
        {
            ActiveModal = "addTableModal";
            await LoadAsync();
            return Page();
        }
        return Result(await _tables.AddTableAsync(
            input.TableNumber, input.Capacity, input.DiningAreaId, input.Shape));
    }

    public async Task<IActionResult> OnPostEditTableAsync(int id, [Bind(Prefix = "EditTable")] TableForm input)
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }
        return Result(await _tables.EditTableAsync(
            id, input.TableNumber, input.Capacity, input.DiningAreaId, input.Shape));
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(int id, string newStatus) =>
        Result(await _tables.ChangeStatusAsync(id, newStatus));

    public async Task<IActionResult> OnPostDeleteTableAsync(int id) =>
        Result(await _tables.DeleteTableAsync(id));
}
