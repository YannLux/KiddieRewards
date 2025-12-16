using System.ComponentModel.DataAnnotations;

namespace PcA.KiddieRewards.Web.Models;

public class Member
{
    public Guid Id { get; set; }

    public Guid FamilyId { get; set; }

    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string AvatarKey { get; set; } = string.Empty;

    [MaxLength(256)]
    public string PinHash { get; set; } = string.Empty;

    public MemberRole Role { get; set; }
        = MemberRole.Child;

    public bool IsActive { get; set; } = true;

    public Family Family { get; set; }
        = null!;

    public ICollection<PointEntry> PointEntriesAsChild { get; set; }
        = new List<PointEntry>();

    public ICollection<PointEntry> PointEntriesCreated { get; set; }
        = new List<PointEntry>();
}

public enum MemberRole
{
    Parent = 1,
    Child = 2
}
