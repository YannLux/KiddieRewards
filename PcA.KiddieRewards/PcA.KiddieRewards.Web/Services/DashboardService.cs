using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;

namespace PcA.KiddieRewards.Web.Services;

public interface IDashboardService
{
    Task<ParentDashboardViewModel> BuildParentDashboardAsync(
        Guid familyId,
        int page = 1,
        Guid? childId = null,
        CancellationToken cancellationToken = default);
}

public class DashboardService(AppDbContext dbContext, IPointsService pointsService) : IDashboardService
{
    private const int HistoryPageSize = 10;

    public async Task<ParentDashboardViewModel> BuildParentDashboardAsync(
        Guid familyId,
        int page = 1,
        Guid? childId = null,
        CancellationToken cancellationToken = default)
    {
        var children = await dbContext.Members
            .AsNoTracking()
            .Where(m => m.FamilyId == familyId && m.Role == MemberRole.Child && m.IsActive)
            .OrderBy(m => m.DisplayName)
            .ToListAsync(cancellationToken);

        var childSummaries = new List<ChildDashboardItem>();

        foreach (var child in children)
        {
            var totals = await pointsService.GetTotalsAsync(child.Id, cancellationToken);
            childSummaries.Add(new ChildDashboardItem(child.Id, child.DisplayName, totals.Plus, totals.Minus, totals.Net));
        }

        var selectedChildId = childId.HasValue && childSummaries.Any(c => c.MemberId == childId.Value)
            ? childId
            : null;

        var activeEntriesQuery = dbContext.PointEntries
            .AsNoTracking()
            .Where(p => p.FamilyId == familyId && p.IsActive);

        var totalPlus = await activeEntriesQuery
            .Where(p => p.Points > 0)
            .SumAsync(p => (int?)p.Points, cancellationToken) ?? 0;

        var totalMinus = await activeEntriesQuery
            .Where(p => p.Points < 0)
            .SumAsync(p => (int?)-p.Points, cancellationToken) ?? 0;

        var weekStart = DateTime.UtcNow.AddDays(-7);
        var weeklyNet = await activeEntriesQuery
            .Where(p => p.CreatedAt >= weekStart)
            .SumAsync(p => (int?)p.Points, cancellationToken) ?? 0;

        var historyQuery = dbContext.PointEntries
            .AsNoTracking()
            .Where(p => p.FamilyId == familyId);

        if (selectedChildId.HasValue)
        {
            historyQuery = historyQuery.Where(p => p.ChildMemberId == selectedChildId.Value);
        }

        var totalCount = await historyQuery.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)HistoryPageSize));
        var currentPage = Math.Clamp(page, 1, totalPages);

        var recentEntries = await historyQuery
            .OrderByDescending(p => p.CreatedAt)
            .Skip((currentPage - 1) * HistoryPageSize)
            .Take(HistoryPageSize)
            .Select(p => new RecentPointEntryItem(
                p.Id,
                p.ChildMemberId,
                p.ChildMember.DisplayName,
                p.CreatedByMember.DisplayName,
                p.Points,
                p.Reason,
                p.CreatedAt,
                p.IsActive,
                p.Type == PointEntryType.Reset || p.IsReset,
                p.Type))
            .ToListAsync(cancellationToken);

        var stats = new ParentDashboardStats(totalPlus, totalMinus, totalPlus - totalMinus, weeklyNet);

        var history = new ParentDashboardHistory(
            recentEntries,
            currentPage,
            totalPages,
            HistoryPageSize,
            totalCount,
            selectedChildId);

        return new ParentDashboardViewModel(familyId, childSummaries, stats, history);
    }
}
