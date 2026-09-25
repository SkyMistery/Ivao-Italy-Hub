using IvaoHub.Core.Auth;
using IvaoHub.Core.Awards;
using IvaoHub.Core.Division;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Preferences;
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
        builder.Property(user => user.Email).HasMaxLength(256);

        // Hours with two decimals, up to ten million of them: IVAO's seconds are turned into hours by the reader.
        builder.Property(user => user.HoursAtc).HasPrecision(9, 2);
        builder.Property(user => user.HoursPilot).HasPrecision(9, 2);
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
        // As wide as ref_ivao_centers.id: a FIR here is one of those, and a column narrower than
        // its source is a truncation waiting for the first centre with a longer identifier.
        builder.Property(position => position.Fir).HasMaxLength(8);
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
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(grant => new { grant.Vid, grant.Effect });

        // A grant to a position (M2): no foreign key, because a position is not a row — it is
        // whoever IVAO lists at that department and level today.
        builder.Ignore(grant => grant.PositionLevels);
        builder.Property(grant => grant.PositionLevelsJson).HasColumnType("json");
        builder.HasIndex(grant => grant.PositionDepartment);
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

internal sealed class PersonalTokenConfiguration : IEntityTypeConfiguration<PersonalToken>
{
    public void Configure(EntityTypeBuilder<PersonalToken> builder)
    {
        builder.ToTable("hub_personal_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Name).HasMaxLength(PersonalTokens.MaxNameLength).IsRequired();
        builder.Property(token => token.Audience).HasMaxLength(TokenAudienceCatalog.MaxKeyLength).IsRequired();
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(token => token.Prefix).HasMaxLength(16).IsRequired();
        builder.HasOne<HubUser>()
            .WithMany()
            .HasForeignKey(token => token.Vid)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => new { token.Vid, token.RevokedAt });
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

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("hub_notifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Type).HasMaxLength(64).IsRequired();
        builder.Property(notification => notification.Address).HasMaxLength(256).IsRequired();
        builder.Property(notification => notification.Locale).HasMaxLength(8).IsRequired();
        builder.Property(notification => notification.DataJson).HasColumnType("json").IsRequired();
        builder.Property(notification => notification.LastError).HasMaxLength(512);

        // The only question the job asks: what is still waiting, oldest first.
        builder.HasIndex(notification => new { notification.Status, notification.Id });
    }
}

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("hub_notification_preferences");
        builder.HasKey(preference => new { preference.Vid, preference.Type });
        builder.Property(preference => preference.Type).HasMaxLength(64);
    }
}

internal sealed class AwardConfiguration : IEntityTypeConfiguration<Award>
{
    public void Configure(EntityTypeBuilder<Award> builder)
    {
        builder.ToTable("hub_awards");
        builder.HasKey(award => award.Id);
        builder.HasRowVersion(award => award.RowVersion);
        builder.HasIndex(award => new { award.OwnerDepartment, award.IsActive });
    }
}

internal sealed class AwardAssignmentConfiguration : IEntityTypeConfiguration<AwardAssignment>
{
    public void Configure(EntityTypeBuilder<AwardAssignment> builder)
    {
        builder.ToTable("hub_award_assignments");
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Reason).HasMaxLength(AwardAssignmentWriteDtoValidator.MaxReasonLength).IsRequired();
        builder.HasRowVersion(assignment => assignment.RowVersion);

        // An award somebody holds cannot vanish from under the register: retiring it is the way.
        builder.HasOne<Award>()
            .WithMany()
            .HasForeignKey(assignment => assignment.AwardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(assignment => assignment.Vid);

        // One signal is answered once. Two people pressing "assign" on the same line at the same time
        // is the race this closes; MariaDB lets any number of rows carry no signal at all.
        builder.HasIndex(assignment => assignment.SignalId).IsUnique();
    }
}

internal sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.ToTable("hub_user_preferences");
        builder.HasKey(preference => new { preference.Vid, preference.Key });
        builder.Property(preference => preference.Key).HasMaxLength(PreferenceCatalog.MaxKeyLength);
        builder.Property(preference => preference.ValueJson).HasColumnType("json").IsRequired();
    }
}
