namespace PcA.KiddieRewards.Web.Models;

public record ChildDashboardItem(Guid MemberId, string DisplayName, int Plus, int Minus, int Net);

public record ParentDashboardStats(int Plus, int Minus, int Net, int WeeklyNet);

public record RecentPointEntryItem(
    Guid PointEntryId,
    Guid ChildMemberId,
    string ChildDisplayName,
    string CreatedByDisplayName,
    int Points,
    string Reason,
    DateTime CreatedAt,
    bool IsActive,
    bool IsReset,
    PointEntryType Type);

public record ParentDashboardViewModel(
    Guid FamilyId,
    IReadOnlyList<ChildDashboardItem> Children,
    ParentDashboardStats Stats,
    IReadOnlyList<RecentPointEntryItem> RecentEntries);
