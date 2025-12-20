using System.ComponentModel.DataAnnotations;

namespace PcA.KiddieRewards.Web.Models;

public class FamilyInvitation
{
    public Guid Id { get; set; }

    public Guid FamilyId { get; set; }

    [MaxLength(32)]
    public string Code { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public bool IsRevoked { get; set; }

    public Guid? CreatedByMemberId { get; set; }

    public Guid? RedeemedByMemberId { get; set; }

    public DateTime? RedeemedAtUtc { get; set; }

    public Family Family { get; set; } = null!;
}
