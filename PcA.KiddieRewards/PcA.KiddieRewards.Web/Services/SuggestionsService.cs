using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;

namespace PcA.KiddieRewards.Web.Services;

public record PointSuggestion(string Reason, int Points, int Uses);

public interface ISuggestionsService
{
    Task<IReadOnlyList<PointSuggestion>> GetSuggestionsAsync(
        Guid familyId,
        string? labelFilter = null,
        int limit = 10,
        CancellationToken cancellationToken = default);
}

public class SuggestionsService(AppDbContext dbContext) : ISuggestionsService
{
    public async Task<IReadOnlyList<PointSuggestion>> GetSuggestionsAsync(
        Guid familyId,
        string? labelFilter = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }

        var normalizedFilter = labelFilter?.Trim().ToLowerInvariant();

        var query = dbContext.PointEntries
            .AsNoTracking()
            .Where(p => p.FamilyId == familyId && p.IsActive && !p.IsReset);

        if (!string.IsNullOrWhiteSpace(normalizedFilter))
        {
            query = query.Where(p => p.Reason.ToLower().Contains(normalizedFilter));
        }

        var grouped = await query
            .GroupBy(p => p.Reason.Trim().ToLower())
            .Select(g => new
            {
                NormalizedReason = g.Key,
                LatestReason = g.OrderByDescending(p => p.CreatedAt).Select(p => p.Reason).First(),
                AveragePoints = g.Average(p => p.Points),
                Uses = g.Count(),
                LatestCreatedAt = g.Max(p => p.CreatedAt)
            })
            .OrderByDescending(x => x.Uses)
            .ThenByDescending(x => x.LatestCreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var suggestions = grouped
            .Select(g => new PointSuggestion(
                g.LatestReason,
                (int)Math.Round(g.AveragePoints),
                g.Uses))
            .ToList();

        return suggestions;
    }
}
