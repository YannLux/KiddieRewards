using System.ComponentModel.DataAnnotations;

namespace PcA.KiddieRewards.Web.Models;

public class PointEntry
{
    public Guid Id { get; set; }

    public Guid FamilyId { get; set; }

    public Guid ChildMemberId { get; set; }

    public Guid CreatedByMemberId { get; set; }

    public int Points { get; set; }

    public PointEntryType Type { get; set; }
        = PointEntryType.GoodPoint;

    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public bool IsReset { get; set; }
        = false;

    public bool IsActive { get; set; }
        = true;

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;

    public Family Family { get; set; } = null!;

    public Member ChildMember { get; set; } = null!;

    public Member CreatedByMember { get; set; } = null!;
}

public enum PointEntryType
{
    [Display(Name = "Bon point")]
    GoodPoint = 0,

    [Display(Name = "Mauvais point")]
    BadPoint = 1,

    [Display(Name = "Récompense")]
    Reward = 2,

    [Display(Name = "Bonus")]
    Bonus = 3,

    [Display(Name = "Reset")]
    Reset = 4
}
