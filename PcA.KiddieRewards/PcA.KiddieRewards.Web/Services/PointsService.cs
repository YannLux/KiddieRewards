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
        PointEntryType type,
        int points,
        string reason,
        CancellationToken cancellationToken = default);

    Task<PointEntry?> UpdatePointEntryAsync(
        Guid pointEntryId,
        PointEntryType type,
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
        PointEntryType type,
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

        var entries = new List<PointEntry>();
        var normalizedPoints = type == PointEntryType.Reset
            ? 0
            : NormalizePointsForType(type, points);
        var trimmedReason = reason.Trim();

        foreach (var childId in targetIds)
        {
            var computedPoints = type == PointEntryType.Reset
                ? await CalculateResetPointsAsync(childId, cancellationToken)
                : normalizedPoints;

            entries.Add(new PointEntry
            {
                Id = Guid.NewGuid(),
                ChildMemberId = childId,
                FamilyId = familyId,
                CreatedByMemberId = createdByMemberId,
                Points = computedPoints,
                Type = type,
                Reason = trimmedReason,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsReset = type == PointEntryType.Reset
            });
        }

        await dbContext.PointEntries.AddRangeAsync(entries, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return entries;
    }

    public async Task<PointEntry?> UpdatePointEntryAsync(
        Guid pointEntryId,
        PointEntryType type,
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

        entry.Type = type;
        entry.IsReset = type == PointEntryType.Reset;
        entry.Points = type == PointEntryType.Reset
            ? await CalculateResetPointsAsync(entry.ChildMemberId, cancellationToken, entry.Id)
            : NormalizePointsForType(type, points);
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

        var hasActiveEntries = await dbContext.PointEntries
            .AsNoTracking()
            .AnyAsync(p => p.FamilyId == familyId && p.ChildMemberId == childMemberId && p.IsActive, cancellationToken);

        if (!hasActiveEntries)
        {
            return null;
        }

        var resetPoints = await CalculateResetPointsAsync(childMemberId, cancellationToken);
        var trimmedReason = reason.Trim();

        var resetEntry = new PointEntry
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            ChildMemberId = childMemberId,
            CreatedByMemberId = createdByMemberId,
            Reason = trimmedReason,
            Points = resetPoints,
            Type = PointEntryType.Reset,
            IsReset = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await dbContext.PointEntries.AddAsync(resetEntry, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return resetEntry;
    }

    private static int NormalizePointsForType(PointEntryType type, int points)
    {
        var absoluteValue = Math.Abs(points);

        return type switch
        {
            PointEntryType.GoodPoint or PointEntryType.Bonus when absoluteValue == 0
                => throw new ArgumentException("Positive point types must use a value greater than zero.", nameof(points)),
            PointEntryType.GoodPoint or PointEntryType.Bonus
                => absoluteValue,
            PointEntryType.BadPoint or PointEntryType.Reward when absoluteValue == 0
                => throw new ArgumentException("Negative point types must use a value lower than zero.", nameof(points)),
            PointEntryType.BadPoint or PointEntryType.Reward
                => -absoluteValue,
            PointEntryType.Reset
                => 0,
            _
                => points
        };
    }

    private async Task<int> CalculateResetPointsAsync(
        Guid childMemberId,
        CancellationToken cancellationToken,
        Guid? excludeEntryId = null)
    {
        var net = await GetNetPointsAsync(childMemberId, excludeEntryId, cancellationToken);
        return -net;
    }

    private async Task<int> GetNetPointsAsync(
        Guid childMemberId,
        Guid? excludeEntryId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.PointEntries
            .AsNoTracking()
            .Where(p => p.ChildMemberId == childMemberId && p.IsActive);

        if (excludeEntryId.HasValue)
        {
            query = query.Where(p => p.Id != excludeEntryId.Value);
        }

        var plus = await query
            .Where(p => p.Points > 0)
            .SumAsync(p => (int?)p.Points, cancellationToken) ?? 0;

        var minusAbsolute = await query
            .Where(p => p.Points < 0)
            .SumAsync(p => (int?)-p.Points, cancellationToken) ?? 0;

        return plus - minusAbsolute;
    }
}
