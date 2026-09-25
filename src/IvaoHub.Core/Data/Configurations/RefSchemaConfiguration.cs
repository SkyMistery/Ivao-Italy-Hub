using IvaoHub.Core.Airspace;
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
        builder.Property(airport => airport.Iata).HasMaxLength(4);
        builder.Property(airport => airport.RunwaysJson).HasColumnType("json");
        builder.Property(airport => airport.RawJson).HasColumnType("json").IsRequired();
        builder.HasIndex(airport => airport.CountryId);
        builder.HasIndex(airport => airport.CenterId);
    }
}

internal sealed class IvaoRunwayConfiguration : IEntityTypeConfiguration<IvaoRunway>
{
    public void Configure(EntityTypeBuilder<IvaoRunway> builder)
    {
        builder.ToTable("ref_ivao_runways");
        builder.HasKey(runway => new { runway.AirportIcao, runway.Designator });
        builder.Property(runway => runway.AirportIcao).HasMaxLength(4);
        builder.Property(runway => runway.Designator).HasMaxLength(8);
    }
}

internal sealed class IvaoAircraftTypeConfiguration : IEntityTypeConfiguration<IvaoAircraftType>
{
    public void Configure(EntityTypeBuilder<IvaoAircraftType> builder)
    {
        builder.ToTable("ref_ivao_aircraft");
        builder.HasKey(type => type.IcaoCode);
        builder.Property(type => type.IcaoCode).HasMaxLength(4).ValueGeneratedNever();
        builder.Property(type => type.IataCode).HasMaxLength(4);
        builder.Property(type => type.Model).HasMaxLength(256).IsRequired();
        builder.Property(type => type.Manufacturer).HasMaxLength(128);
        builder.Property(type => type.Description).HasMaxLength(64);
        builder.Property(type => type.WakeTurbulence).HasMaxLength(2);
        builder.Property(type => type.Military).HasMaxLength(16);
        builder.Property(type => type.RawJson).HasColumnType("json").IsRequired();

        // The editor of a tour offers the types by manufacturer and by model.
        builder.HasIndex(type => type.Manufacturer);
    }
}

internal sealed class IvaoAircraftEquipmentConfiguration : IEntityTypeConfiguration<IvaoAircraftEquipment>
{
    public void Configure(EntityTypeBuilder<IvaoAircraftEquipment> builder)
    {
        builder.ToTable("ref_ivao_aircraft_equipments");
        builder.HasKey(equipment => equipment.Id);
        builder.Property(equipment => equipment.Id).HasMaxLength(4).ValueGeneratedNever();
        builder.Property(equipment => equipment.Name).HasMaxLength(256).IsRequired();
    }
}

internal sealed class IvaoTransponderTypeConfiguration : IEntityTypeConfiguration<IvaoTransponderType>
{
    public void Configure(EntityTypeBuilder<IvaoTransponderType> builder)
    {
        builder.ToTable("ref_ivao_transponder_types");
        builder.HasKey(transponder => transponder.Id);
        builder.Property(transponder => transponder.Id).HasMaxLength(4).ValueGeneratedNever();
        builder.Property(transponder => transponder.Name).HasMaxLength(256).IsRequired();
        builder.Property(transponder => transponder.Kind).HasMaxLength(64);
    }
}

/// <summary>
/// The ATC positions of the world (M3, A2): an airport position has its airport, a sector its FIR, and the directory
/// reads them by kind and joins the airports on the first.
/// </summary>
internal sealed class IvaoAtcPositionConfiguration : IEntityTypeConfiguration<IvaoAtcPosition>
{
    public void Configure(EntityTypeBuilder<IvaoAtcPosition> builder)
    {
        builder.ToTable("ref_ivao_atc_positions");
        builder.HasKey(position => position.Callsign);
        builder.Property(position => position.Callsign).HasMaxLength(IvaoAtcPosition.MaxCallsignLength).ValueGeneratedNever();
        builder.Property(position => position.PositionType).HasMaxLength(IvaoAtcPosition.MaxPositionTypeLength).IsRequired();
        builder.Property(position => position.AirportIcao).HasMaxLength(IvaoAtcPosition.MaxAirportLength);
        builder.Property(position => position.CenterId).HasMaxLength(IvaoAtcPosition.MaxCenterLength);
        builder.Property(position => position.Name).HasMaxLength(IvaoAtcPosition.MaxNameLength).IsRequired();
        builder.Property(position => position.RawJson).HasColumnType("json").IsRequired();
        builder.HasIndex(position => position.PositionType);
        builder.HasIndex(position => position.AirportIcao);
        builder.HasIndex(position => position.CenterId);
    }
}

/// <summary>
/// The outlines of the flight information regions. In <c>ref_</c> like the IVAO snapshots because it
/// is the same kind of thing — reference data fetched from outside, never edited here — even though
/// it does not come from IVAO (decision note of 16 September 2026).
/// </summary>
internal sealed class FirBoundaryConfiguration : IEntityTypeConfiguration<FirBoundary>
{
    public void Configure(EntityTypeBuilder<FirBoundary> builder)
    {
        builder.ToTable("ref_firs");
        builder.HasKey(fir => fir.Id);
        builder.Property(fir => fir.Id).HasMaxLength(16).ValueGeneratedNever();
        builder.Property(fir => fir.Region).HasMaxLength(32);
        builder.Property(fir => fir.GeometryJson).HasColumnType("json").IsRequired();
    }
}
