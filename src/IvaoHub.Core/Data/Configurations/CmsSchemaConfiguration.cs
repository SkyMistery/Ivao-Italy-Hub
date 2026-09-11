using IvaoHub.Core.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IvaoHub.Core.Data.Configurations;

/// <summary>Schema <c>cms_</c>: the editorial core and the three projections.</summary>
internal sealed class ContentEntryConfiguration : IEntityTypeConfiguration<ContentEntry>
{
    public void Configure(EntityTypeBuilder<ContentEntry> builder)
    {
        builder.ToTable("cms_contents");
        builder.HasKey(content => content.Id);
        builder.Property(content => content.Slug).HasMaxLength(160).IsRequired();
        builder.Property(content => content.BodyJson).HasColumnType("json").IsRequired();
        builder.Property(content => content.Category).HasMaxLength(64);
        builder.HasRowVersion(content => content.RowVersion);

        // MariaDB has no filtered indexes, so a template and a page may share a slug but two pages
        // may not (design M0 section 5.1).
        builder.HasIndex(content => new { content.Kind, content.Slug, content.IsTemplate }).IsUnique();
        builder.HasIndex(content => new { content.Kind, content.Status });
        builder.HasIndex(content => new { content.OwnerDepartment, content.Status });
        builder.HasIndex(content => content.TemplateId);
    }
}

internal sealed class ContentVersionConfiguration : IEntityTypeConfiguration<ContentVersion>
{
    public void Configure(EntityTypeBuilder<ContentVersion> builder)
    {
        builder.ToTable("cms_content_versions");
        builder.HasKey(version => version.Id);
        builder.Property(version => version.BodyJson).HasColumnType("json").IsRequired();
        builder.Property(version => version.Changelog).HasMaxLength(512);
        builder.HasOne(version => version.Content)
            .WithMany()
            .HasForeignKey(version => version.ContentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(version => new { version.ContentId, version.Version }).IsUnique();
    }
}

internal sealed class ContentCategoryConfiguration : IEntityTypeConfiguration<ContentCategory>
{
    public void Configure(EntityTypeBuilder<ContentCategory> builder)
    {
        builder.ToTable("cms_categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Key)
            .HasMaxLength(CategoryWriteDtoValidator.MaxKeyLength)
            .IsRequired();
        builder.HasRowVersion(category => category.RowVersion);

        // One word per department and per kind: two departments may both file under "guides" and
        // mean two different shelves, one department may not have the same shelf twice.
        builder.HasIndex(category => new { category.Kind, category.OwnerDepartment, category.Key })
            .IsUnique();
        builder.HasIndex(category => new { category.Kind, category.IsActive });
    }
}

internal sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("cms_menu_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Path)
            .HasMaxLength(MenuItemWriteDtoValidator.MaxPathLength)
            .IsRequired();
        builder.Property(item => item.Icon).HasMaxLength(MenuItemWriteDtoValidator.MaxIconLength);
        builder.HasRowVersion(item => item.RowVersion);

        // How the menu is read: one scope at a time, top level entries first, in their own order.
        builder.HasIndex(item => new { item.Scope, item.ParentId, item.Sort });
        builder.HasIndex(item => item.IsActive);
    }
}

internal sealed class LinkConfiguration : IEntityTypeConfiguration<Link>
{
    public void Configure(EntityTypeBuilder<Link> builder)
    {
        builder.ToTable("cms_links");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Url).HasMaxLength(1024).IsRequired();
        builder.Property(link => link.Category).HasMaxLength(64);
        builder.HasRowVersion(link => link.RowVersion);
        builder.HasIndex(link => new { link.OwnerDepartment, link.IsActive });
        builder.HasIndex(link => link.Category);
    }
}

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("cms_media");
        builder.HasKey(media => media.Id);
        builder.Property(media => media.FileName).HasMaxLength(255).IsRequired();
        builder.Property(media => media.StoredName).HasMaxLength(128).IsRequired();
        builder.Property(media => media.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(media => media.Category).HasMaxLength(64);
        builder.HasRowVersion(media => media.RowVersion);

        // Two files never share a name on disk, and the database says so rather than trusting the
        // generator that made it.
        builder.HasIndex(media => media.StoredName).IsUnique();
        builder.HasIndex(media => new { media.OwnerDepartment, media.DeletedAt });
        builder.HasIndex(media => media.Category);
    }
}

internal sealed class SearchIndexEntryConfiguration : IEntityTypeConfiguration<SearchIndexEntry>
{
    public void Configure(EntityTypeBuilder<SearchIndexEntry> builder)
    {
        builder.ToTable("cms_search_index");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.SourceModule).HasMaxLength(32).IsRequired();
        builder.Property(entry => entry.SourceId).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.Locale).HasMaxLength(8).IsRequired();
        builder.Property(entry => entry.Kind).HasMaxLength(32).IsRequired();
        builder.Property(entry => entry.Url).HasMaxLength(1024).IsRequired();
        builder.Property(entry => entry.Title).HasMaxLength(512).IsRequired();
        builder.Property(entry => entry.Text).HasColumnType("mediumtext").IsRequired();

        // One row per source row and per language: a FULLTEXT index that works for any set of
        // languages, without a column hardcoded per language (design M0 section 3.6).
        builder.HasIndex(entry => new { entry.SourceModule, entry.SourceId, entry.Locale }).IsUnique();
        builder.HasIndex(entry => new { entry.Title, entry.Text })
            .HasDatabaseName("ix_cms_search_index_fulltext")
            .IsFullText();

        // What breaks a tie in the results, and what a language filter narrows to first.
        builder.HasIndex(entry => new { entry.Locale, entry.UpdatedAt });
    }
}

internal sealed class CalendarEntryConfiguration : IEntityTypeConfiguration<CalendarEntry>
{
    public void Configure(EntityTypeBuilder<CalendarEntry> builder)
    {
        builder.ToTable("cms_calendar_entries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Kind).HasMaxLength(32).IsRequired();
        builder.Property(entry => entry.SourceModule).HasMaxLength(32).IsRequired();
        builder.Property(entry => entry.SourceId).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.Url).HasMaxLength(1024).IsRequired();
        builder.HasIndex(entry => new { entry.SourceModule, entry.SourceId }).IsUnique();
        builder.HasIndex(entry => entry.StartsAtUtc);
        builder.HasIndex(entry => new { entry.OwnerDepartment, entry.StartsAtUtc });
    }
}

internal sealed class CalendarKindConfiguration : IEntityTypeConfiguration<CalendarKind>
{
    public void Configure(EntityTypeBuilder<CalendarKind> builder)
    {
        builder.ToTable("cms_calendar_kinds");
        builder.HasKey(kind => kind.Id);
        builder.Property(kind => kind.Key)
            .HasMaxLength(CalendarKindWriteDtoValidator.MaxKeyLength)
            .IsRequired();
        builder.Property(kind => kind.Colour).HasMaxLength(16).IsRequired();
        builder.HasRowVersion(kind => kind.RowVersion);

        // One word for the whole division, which is the entire point of the table: a unique index
        // on the key alone, where a category has one per department and per kind.
        builder.HasIndex(kind => kind.Key).IsUnique();
        builder.HasIndex(kind => kind.IsActive);
    }
}

internal sealed class AwardSignalConfiguration : IEntityTypeConfiguration<AwardSignal>
{
    public void Configure(EntityTypeBuilder<AwardSignal> builder)
    {
        builder.ToTable("cms_award_signals");
        builder.HasKey(signal => signal.Id);
        builder.Property(signal => signal.SourceModule).HasMaxLength(32).IsRequired();
        builder.Property(signal => signal.SourceId).HasMaxLength(64).IsRequired();
        builder.Property(signal => signal.Reason).HasMaxLength(256).IsRequired();
        builder.HasIndex(signal => new { signal.SourceModule, signal.SourceId, signal.Vid }).IsUnique();
        builder.HasIndex(signal => new { signal.Vid, signal.Status });
    }
}

internal sealed class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> builder)
    {
        builder.ToTable("cms_contact_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Subject)
            .HasMaxLength(ContactSubmitDtoValidator.MaxSubjectLength)
            .IsRequired();
        builder.Property(message => message.Body).HasColumnType("text").IsRequired();
        builder.HasRowVersion(message => message.RowVersion);

        // The queue of one department, newest first, is the only way this table is ever read.
        builder.HasIndex(message => new { message.OwnerDepartment, message.Status, message.CreatedAt });
    }
}
