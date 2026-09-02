using IvaoHub.Core.Ivao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IvaoHub.Core.Data.Configurations;

/// <summary>
/// Schema <c>ref_</c>: read only snapshots of the IVAO reference data. They live in the core
/// because several modules need them and the core may not depend on an optional module.
/// </summary>
internal sealed class IvaoCenterConfiguration : IEntityTypeConfiguration<IvaoCenter>
{
    public void Configure(EntityTypeBuilder<IvaoCenter> builder)
    {
        builder.ToTable("ref_ivao_centers");
        builder.HasKey(center => center.Id);
        builder.Property(center => center.Id).HasMaxLength(8).ValueGeneratedNever();
        builder.Property(center => center.Name).HasMaxLength(256).IsRequired();
        builder.Property(center => center.CountryId).HasMaxLength(3).IsRequired();
        builder.Property(center => center.RawJson).HasColumnType("json").IsRequired();
        builder.HasIndex(center => center.CountryId);
    }
}

internal sealed class IvaoAirportConfiguration : IEntityTypeConfiguration<IvaoAirport>
{
    public void Configure(EntityTypeBuilder<IvaoAirport> builder)
    {
        builder.ToTable("ref_ivao_airports");
        builder.HasKey(airport => airport.Icao);
        builder.Property(airport => airport.Icao).HasMaxLength(4).ValueGeneratedNever();
        builder.Property(airport => airport.Name).HasMaxLength(256).IsRequired();
        builder.Property(airport => airport.CountryId).HasMaxLength(3).IsRequired();
        builder.Property(airport => airport.CenterId).HasMaxLength(8);
        builder.Property(airport => airport.RunwaysJson).HasColumnType("json");
        builder.Property(airport => airport.RawJson).HasColumnType("json").IsRequired();
        builder.HasIndex(airport => airport.CountryId);
        builder.HasIndex(airport => airport.CenterId);
    }
}
