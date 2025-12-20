using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Models;

namespace PcA.KiddieRewards.Web.Data;

// Change to include IdentityRole so AddRoles<IdentityRole>() can resolve IRoleStore<IdentityRole>
public class AppDbContext(DbContextOptions options)
    : IdentityDbContext<IdentityUser, IdentityRole, string>(options)
{
    public DbSet<Family> Families => Set<Family>();

    public DbSet<FamilyInvitation> FamilyInvitations => Set<FamilyInvitation>();

    public DbSet<Member> Members => Set<Member>();

    public DbSet<PointEntry> PointEntries => Set<PointEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Family>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.Name)
                  .IsRequired()
                  .HasMaxLength(200);
            entity.Property(f => f.IsActive)
                  .HasDefaultValue(true);
        });

        builder.Entity<Member>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.DisplayName)
                  .IsRequired()
                  .HasMaxLength(100);
            entity.Property(m => m.AvatarKey)
                  .IsRequired()
                  .HasMaxLength(100);
            entity.Property(m => m.PinHash)
                  .IsRequired()
                  .HasMaxLength(256);
            entity.Property(m => m.Role)
                  .IsRequired();
            entity.Property(m => m.IsActive)
                  .HasDefaultValue(true);

            entity.HasIndex(m => m.FamilyId);
            entity.HasIndex(m => new { m.FamilyId, m.PinHash }).IsUnique();

            entity.HasOne(m => m.Family)
                  .WithMany(f => f.Members)
                  .HasForeignKey(m => m.FamilyId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FamilyInvitation>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Code)
                  .IsRequired()
                  .HasMaxLength(32);
            entity.Property(i => i.CreatedAtUtc)
                  .HasColumnType("datetime2");
            entity.Property(i => i.ExpiresAtUtc)
                  .HasColumnType("datetime2");
            entity.Property(i => i.IsRevoked)
                  .HasDefaultValue(false);
            entity.HasIndex(i => i.Code)
                  .IsUnique();
            entity.HasIndex(i => i.FamilyId);

            entity.HasOne(i => i.Family)
                  .WithMany()
                  .HasForeignKey(i => i.FamilyId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PointEntry>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Points).IsRequired();
            entity.Property(p => p.Type)
                  .IsRequired()
                  .HasDefaultValue(PointEntryType.GoodPoint)
                  .HasConversion<int>();
            entity.Property(p => p.Reason)
                  .IsRequired()
                  .HasMaxLength(500);
            entity.Property(p => p.CreatedAt)
                  .HasColumnType("datetime2");
            entity.Property(p => p.IsReset)
                  .HasDefaultValue(false);
            entity.Property(p => p.IsActive)
                  .HasDefaultValue(true);

            entity.HasIndex(p => p.FamilyId);
            entity.HasIndex(p => p.ChildMemberId);
            entity.HasIndex(p => p.Type);
            entity.HasIndex(p => p.IsActive);

            entity.HasOne(p => p.Family)
                  .WithMany(f => f.PointEntries)
                  .HasForeignKey(p => p.FamilyId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.ChildMember)
                  .WithMany(m => m.PointEntriesAsChild)
                  .HasForeignKey(p => p.ChildMemberId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.CreatedByMember)
                  .WithMany(m => m.PointEntriesCreated)
                  .HasForeignKey(p => p.CreatedByMemberId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
