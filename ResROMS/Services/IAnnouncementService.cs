namespace ResROMS.Services;

public sealed record AnnouncementInput(string Title, string Body, string Audience,
    DateTime StartLocal, DateTime? EndLocal);
public sealed record AnnouncementView(int Id, string Title, string Body, string Audience,
    DateTime StartLocal, DateTime? EndLocal, string Status, bool IsPublished,
    bool IsArchived, bool WasPublished);

public interface IAnnouncementService
{
    Task<IReadOnlyList<AnnouncementView>> GetForAdminAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AnnouncementView>> GetLiveForRoleAsync(string role, CancellationToken ct = default);
    Task<OperationResult> CreateAsync(AnnouncementInput input, int actorId, CancellationToken ct = default);
    Task<OperationResult> EditAsync(int id, AnnouncementInput input, CancellationToken ct = default);
    Task<OperationResult> SetPublishedAsync(int id, bool published, CancellationToken ct = default);
    Task<OperationResult> SetArchivedAsync(int id, bool archived, CancellationToken ct = default);
    Task<OperationResult> DeleteDraftAsync(int id, CancellationToken ct = default);
}
