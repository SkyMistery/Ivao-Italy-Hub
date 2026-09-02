using IvaoHub.Core.Auth;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IvaoHub.Core.Data.Configurations;

/// <summary>
/// Schema <c>hub_</c>: identity and permissions, shared by every module. On MariaDB a schema is
/// only a table prefix, so the prefix is spelled out here rather than inferred.
/// </summary>
internal sealed class HubUserConfiguration : IEntityTypeConfiguration<HubUser>
{
    public void Configure(EntityTypeBuilder<HubUser> builder)
    {
        builder.ToTable("hub_users");
        builder.HasKey(user => user.Vid);
        builder.Property(user => user.Vid).ValueGeneratedNever();
        builder.Property(user => user.FirstName).HasMaxLength(128).IsRequired();
        builder.Property(user => user.LastName).HasMaxLength(128).IsRequired();
        builder.Property(user => user.PublicNickname).HasMaxLength(128);
        builder.Property(user => user.DivisionCode).HasMaxLength(3);
        builder.Property(user => user.Country).HasMaxLength(3);
        builder.Property(user => user.DiscordId).HasMaxLength(32);
        builder.Property(user => user.Locale).HasMaxLength(8);
        builder.Property(user => user.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.HasRowVersion(user => user.RowVersion);
        builder.HasIndex(user => user.IsStaff);
    }
}

internal sealed class UserStaffPositionConfiguration : IEntityTypeConfiguration<UserStaffPosition>
{
    public void Configure(EntityTypeBuilder<UserStaffPosition> builder)
    {
        builder.ToTable("hub_user_staff_positions");
        builder.HasKey(position => new { position.Vid, position.Position });
        builder.Property(position => position.Position).HasMaxLength(32);
        builder.Property(position => position.Fir).HasMaxLength(4);
        builder.HasOne(position => position.User)
            .WithMany(user => user.StaffPositions)
            .HasForeignKey(position => position.Vid)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(position => position.Department);
    }
}

internal sealed class UserGrantConfiguration : IEntityTypeConfiguration<UserGrant>
{
    public void Configure(EntityTypeBuilder<UserGrant> builder)
    {
        builder.ToTable("hub_user_grants");
        builder.HasKey(grant => grant.Id);
        builder.Property(grant => grant.Value).HasMaxLength(64).IsRequired();
        builder.Property(grant => grant.Reason).HasMaxLength(512);
        builder.HasRowVersion(grant => grant.RowVersion);
        builder.HasOne(grant => grant.User)
            .WithMany()
            .HasForeignKey(grant => grant.Vid)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(grant => new { grant.Vid, grant.Effect });
    }
}

internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.ToTable("hub_user_tokens");
        builder.HasKey(token => token.Vid);
        builder.Property(token => token.Vid).ValueGeneratedNever();
        builder.Property(token => token.AccessTokenEnc).HasColumnType("text").IsRequired();
        builder.Property(token => token.RefreshTokenEnc).HasColumnType("text");
        builder.Property(token => token.Scopes).HasMaxLength(512);
        builder.HasOne(token => token.User)
            .WithOne()
            .HasForeignKey<UserToken>(token => token.Vid)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DivisionSettingConfiguration : IEntityTypeConfiguration<DivisionSetting>
{
    public void Configure(EntityTypeBuilder<DivisionSetting> builder)
    {
        builder.ToTable("hub_division_settings");
        builder.HasKey(setting => setting.Key);
        builder.Property(setting => setting.Key).HasMaxLength(128);
        builder.Property(setting => setting.ValueJson).HasColumnType("json").IsRequired();
    }
}

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("hub_audit_log");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Action).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.Entity).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.EntityId).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.BeforeJson).HasColumnType("json");
        builder.Property(entry => entry.AfterJson).HasColumnType("json");
        builder.Property(entry => entry.Ip).HasMaxLength(45);
        builder.HasIndex(entry => new { entry.Entity, entry.EntityId });
        builder.HasIndex(entry => new { entry.Vid, entry.At });
    }
}

internal sealed class JobLogEntryConfiguration : IEntityTypeConfiguration<JobLogEntry>
{
    public void Configure(EntityTypeBuilder<JobLogEntry> builder)
    {
        builder.ToTable("hub_jobs_log");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Job).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.Status).HasMaxLength(16).IsRequired();
        builder.Property(entry => entry.Message).HasColumnType("text");
        builder.HasIndex(entry => new { entry.Job, entry.StartedAt });
    }
}
