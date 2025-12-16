using System.ComponentModel.DataAnnotations;

namespace PcA.KiddieRewards.Web.Models;

public class Family
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Member> Members { get; set; } = new List<Member>();

    public ICollection<PointEntry> PointEntries { get; set; } = new List<PointEntry>();
}
