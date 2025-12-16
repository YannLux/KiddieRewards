using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;

namespace PcA.KiddieRewards.Web.Services;

public record PointsTotals(int Plus, int Minus, int Net);

public interface IPointsService
{
    Task<IReadOnlyList<PointEntry>> AddPointsAsync(
        Guid familyId,
        Guid createdByMemberId,
        IEnumerable<Guid> childMemberIds,
        int points,
        string reason,
        CancellationToken cancellationToken = default);

    Task<PointEntry?> UpdatePointEntryAsync(
        Guid pointEntryId,
        int points,
        string reason,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    Task<PointsTotals> GetTotalsAsync(Guid childMemberId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PointEntry>> GetHistoryAsync(
        Guid childMemberId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<PointEntry?> ApplyResetAsync(
        Guid familyId,
        Guid childMemberId,
        Guid createdByMemberId,
        string reason,
        CancellationToken cancellationToken = default);
}

public class PointsService(AppDbContext dbContext) : IPointsService
{
    public async Task<IReadOnlyList<PointEntry>> AddPointsAsync(
        Guid familyId,
        Guid createdByMemberId,
        IEnumerable<Guid> childMemberIds,
        int points,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var targetIds = childMemberIds.Distinct().ToList();
        if (targetIds.Count == 0)
        {
            throw new ArgumentException("At least one child identifier is required.", nameof(childMemberIds));
        }

        var activeMembers = await dbContext.Members
            .Where(m => targetIds.Contains(m.Id) && m.FamilyId == familyId && m.IsActive)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (activeMembers.Count != targetIds.Count)
        {
            throw new InvalidOperationException("One or more children are invalid or inactive.");
        }

        var entries = targetIds
            .Select(childId => new PointEntry
            {
                Id = Guid.NewGuid(),
                ChildMemberId = childId,
                FamilyId = familyId,
                CreatedByMemberId = createdByMemberId,
                Points = points,
                Reason = reason.Trim(),
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsReset = false
            })
            .ToList();

        await dbContext.PointEntries.AddRangeAsync(entries, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return entries;
    }

    public async Task<PointEntry?> UpdatePointEntryAsync(
        Guid pointEntryId,
        int points,
        string reason,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var entry = await dbContext.PointEntries
            .FirstOrDefaultAsync(p => p.Id == pointEntryId, cancellationToken);

        if (entry is null)
        {
            return null;
        }

        entry.Points = points;
        entry.Reason = reason.Trim();

        if (isActive.HasValue)
        {
            entry.IsActive = isActive.Value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return entry;
    }

    public async Task<PointsTotals> GetTotalsAsync(Guid childMemberId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.PointEntries
            .AsNoTracking()
            .Where(p => p.ChildMemberId == childMemberId && p.IsActive);

        var plus = await query
            .Where(p => p.Points > 0)
            .SumAsync(p => (int?)p.Points, cancellationToken) ?? 0;

        var minusAbsolute = await query
            .Where(p => p.Points < 0)
            .SumAsync(p => (int?)-p.Points, cancellationToken) ?? 0;

        var net = plus - minusAbsolute;

        return new PointsTotals(plus, minusAbsolute, net);
    }

    public async Task<IReadOnlyList<PointEntry>> GetHistoryAsync(
        Guid childMemberId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.PointEntries
            .AsNoTracking()
            .Where(p => p.ChildMemberId == childMemberId)
            .OrderByDescending(p => p.CreatedAt);

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive).OrderByDescending(p => p.CreatedAt);
        }

        var history = await query.ToListAsync(cancellationToken);
        return history;
    }

    public async Task<PointEntry?> ApplyResetAsync(
        Guid familyId,
        Guid childMemberId,
        Guid createdByMemberId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var existingEntries = await dbContext.PointEntries
            .Where(p => p.FamilyId == familyId && p.ChildMemberId == childMemberId && p.IsActive)
            .ToListAsync(cancellationToken);

        if (existingEntries.Count == 0)
        {
            return null;
        }

        foreach (var entry in existingEntries)
        {
            entry.IsActive = false;
        }

        var resetEntry = new PointEntry
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            ChildMemberId = childMemberId,
            CreatedByMemberId = createdByMemberId,
            Reason = reason.Trim(),
            Points = 0,
            IsReset = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await dbContext.PointEntries.AddAsync(resetEntry, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return resetEntry;
    }
}
