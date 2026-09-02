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
