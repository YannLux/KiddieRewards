using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;

namespace PcA.KiddieRewards.Web.Services;

public class DataSeeder(
    AppDbContext dbContext,
    UserManager<IdentityUser> userManager,
    IPinHasher pinHasher)
{
    private static readonly Guid FamilyId = Guid.Parse("e54c6db0-4c59-4d01-8c60-2fa8d969eb8f");
    private static readonly Guid ParentMemberId = Guid.Parse("5d4cd6cc-6d8e-4f50-9a85-a3d9a7c305d6");
    private static readonly Guid Child1MemberId = Guid.Parse("5b08a2a0-7b2c-4a5c-b6a1-1a5c8b4b2ee1");
    private static readonly Guid Child2MemberId = Guid.Parse("5a2dceaa-2056-4c3a-94c2-5784d6e8e2d1");

    public async Task SeedAsync()
    {
        await dbContext.Database.EnsureCreatedAsync();
        await EnsurePointEntryTypeColumnAsync();
        await dbContext.Database.MigrateAsync();

        await EnsureFamilyAsync();
        await EnsureMembersAsync();
        await EnsureOwnerUserAsync();
    }

    private async Task EnsurePointEntryTypeColumnAsync()
    {
        const string addColumnSql = """
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'PointEntries' AND COLUMN_NAME = 'Type')
            BEGIN
                ALTER TABLE [PointEntries] ADD [Type] INT NOT NULL CONSTRAINT [DF_PointEntries_Type] DEFAULT 0;
            END
            """;

        const int resetTypeValue = (int)PointEntryType.Reset;
        var syncResetSql = $"UPDATE [PointEntries] SET [Type] = {resetTypeValue} WHERE [IsReset] = 1 AND [Type] <> {resetTypeValue}";

        await dbContext.Database.ExecuteSqlRawAsync(addColumnSql);
        await dbContext.Database.ExecuteSqlRawAsync(syncResetSql);
    }

    private async Task EnsureFamilyAsync()
    {
        if (await dbContext.Families.AnyAsync(f => f.Id == FamilyId))
        {
            return;
        }

        dbContext.Families.Add(new Family
        {
            Id = FamilyId,
            Name = "Famille Demo"
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task EnsureMembersAsync()
    {
        if (await dbContext.Members.AnyAsync(m => m.FamilyId == FamilyId))
        {
            return;
        }

        var parent = new Member
        {
            Id = ParentMemberId,
            FamilyId = FamilyId,
            DisplayName = "Parent Demo",
            AvatarKey = "parent-star",
            PinHash = pinHasher.HashPin("1234"),
            Role = MemberRole.Parent
        };

        var child1 = new Member
        {
            Id = Child1MemberId,
            FamilyId = FamilyId,
            DisplayName = "Léo",
            AvatarKey = "lion",
            PinHash = pinHasher.HashPin("1111"),
            Role = MemberRole.Child
        };

        var child2 = new Member
        {
            Id = Child2MemberId,
            FamilyId = FamilyId,
            DisplayName = "Mia",
            AvatarKey = "panda",
            PinHash = pinHasher.HashPin("2222"),
            Role = MemberRole.Child
        };

        dbContext.Members.AddRange(parent, child1, child2);
        await dbContext.SaveChangesAsync();
    }

    private async Task EnsureOwnerUserAsync()
    {
        const string ownerEmail = "owner@demo.local";
        const string defaultPassword = "P@ssw0rd!";

        if (await userManager.FindByEmailAsync(ownerEmail) is not null)
        {
            return;
        }

        var user = new IdentityUser
        {
            UserName = ownerEmail,
            Email = ownerEmail,
            EmailConfirmed = true,
        };

        await userManager.CreateAsync(user, defaultPassword);
    }
}
