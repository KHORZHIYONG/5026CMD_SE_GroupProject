using Microsoft.EntityFrameworkCore;
using ResROMS.Data;
using ResROMS.Models;

namespace ResROMS.Services;

public sealed class AnnouncementService : IAnnouncementService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AnnouncementService> _logger;

    public AnnouncementService(AppDbContext db, ILogger<AnnouncementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AnnouncementView>> GetForAdminAsync(CancellationToken ct = default)
    {
        var rows = await _db.Announcements.AsNoTracking()
            .OrderByDescending(a => a.CreatedUtc).ThenByDescending(a => a.Id)
            .Take(250).ToListAsync(ct);
        var now = DateTime.UtcNow;
        return rows.Select(a => Map(a, now)).ToArray();
    }

    public async Task<IReadOnlyList<AnnouncementView>> GetLiveForRoleAsync(string role, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.Announcements.AsNoTracking()
            .Where(a => a.IsPublished && !a.IsArchived && a.StartsUtc <= now
                && (!a.EndsUtc.HasValue || a.EndsUtc.Value > now)
                && (a.Audience == "All" || a.Audience == role))
            .OrderByDescending(a => a.StartsUtc).Take(50).ToListAsync(ct);
        return rows.Select(a => Map(a, now)).ToArray();
    }

    public async Task<OperationResult> CreateAsync(AnnouncementInput input, int actorId, CancellationToken ct = default)
    {
        var result = Validate(input);
        if (!result.Success) return result;
        if (actorId <= 0) return new(false, "Please sign in again.");
        try
        {
            var a = new Announcement { CreatedByUserId = actorId };
            Assign(a, input);
            _db.Announcements.Add(a);
            await _db.SaveChangesAsync(ct);
            return new(true, $"Announcement #{a.Id} saved as a draft.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Creating announcement failed");
            return new(false, "Could not save announcement. Please retry.");
        }
    }

    public async Task<OperationResult> EditAsync(int id, AnnouncementInput input, CancellationToken ct = default)
    {
        var result = Validate(input);
        if (!result.Success) return result;
        try
        {
            var a = await _db.Announcements.SingleOrDefaultAsync(a => a.Id == id, ct);
            if (a is null) return new(false, "Announcement not found.");
            if (a.IsArchived) return new(false, "Restore this announcement before editing it.");
            Assign(a, input);
            a.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new(true, "Announcement updated.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Editing announcement failed");
            return new(false, "Could not update announcement. Please retry.");
        }
    }

    public async Task<OperationResult> SetPublishedAsync(int id, bool published, CancellationToken ct = default)
    {
        var a = await _db.Announcements.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (a is null) return new(false, "Announcement not found.");
        if (a.IsArchived) return new(false, "Restore the announcement first.");
        if (published && a.EndsUtc.HasValue && a.EndsUtc <= DateTime.UtcNow)
            return new(false, "Cannot publish an expired announcement. Edit its end date first.");
        a.IsPublished = published;
        if (published) a.FirstPublishedUtc ??= DateTime.UtcNow;
        a.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new(true, published ? "Announcement published (or scheduled for its start time)." : "Announcement unpublished.");
    }

    public async Task<OperationResult> SetArchivedAsync(int id, bool archived, CancellationToken ct = default)
    {
        var a = await _db.Announcements.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (a is null) return new(false, "Announcement not found.");
        a.IsArchived = archived;
        if (archived) a.IsPublished = false;
        a.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new(true, archived ? "Announcement archived and unpublished." : "Announcement restored as a draft.");
    }

    public async Task<OperationResult> DeleteDraftAsync(int id, CancellationToken ct = default)
    {
        var a = await _db.Announcements.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (a is null) return new(false, "Announcement not found.");
        // Keep history for anything that has ever been made visible.
        if (a.FirstPublishedUtc.HasValue) return new(false, "Published announcements must be archived, not deleted.");
        _db.Announcements.Remove(a);
        await _db.SaveChangesAsync(ct);
        return new(true, "Unpublished draft deleted.");
    }

    private static OperationResult Validate(AnnouncementInput input)
    {
        var title = (input.Title ?? "").Trim();
        var body = (input.Body ?? "").Trim();
        if (title.Length is < 1 or > 140 || body.Length is < 1 or > 2000)
            return new(false, "Title must be 1–140 characters and message 1–2,000 characters.");
        if (input.Audience is not ("All" or "Customer" or "Staff"))
            return new(false, "Choose a valid audience.");
        if (input.StartLocal.Year is < 2000 or > 2100
            || (input.EndLocal.HasValue && (input.EndLocal.Value <= input.StartLocal
                || input.EndLocal.Value.Year > 2100 || input.EndLocal.Value.Year < 2000)))
            return new(false, "End time must be after start time. Use dates from 2000–2100.");
        return new(true, "Valid");
    }

    private static void Assign(Announcement a, AnnouncementInput input)
    {
        a.Title = input.Title.Trim();
        a.Body = input.Body.Trim();
        a.Audience = input.Audience;
        a.StartsUtc = ReservationClock.ToUtc(input.StartLocal);
        a.EndsUtc = input.EndLocal.HasValue ? ReservationClock.ToUtc(input.EndLocal.Value) : null;
    }

    private static AnnouncementView Map(Announcement a, DateTime now) => new(
        a.Id, a.Title, a.Body, a.Audience,
        ReservationClock.ToLocal(a.StartsUtc),
        a.EndsUtc.HasValue ? ReservationClock.ToLocal(a.EndsUtc.Value) : null,
        a.IsArchived ? "Archived" : !a.IsPublished ? "Draft" : a.StartsUtc > now ? "Scheduled"
            : a.EndsUtc.HasValue && a.EndsUtc <= now ? "Expired" : "Live",
        a.IsPublished, a.IsArchived, a.FirstPublishedUtc.HasValue);
}
