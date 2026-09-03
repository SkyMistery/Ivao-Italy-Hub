using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Data;

/// <summary>
/// The database of the core. Every module gets its own context and its own migration history
/// table; there is never a foreign key between two contexts, only unconstrained <c>vid</c> and
/// <c>icao</c> columns (plan section 16.12).
/// </summary>
public class HubDbContext : DbContext
{
    /// <summary>The MariaDB version of production. Never auto detected: a build must be reproducible.</summary>
    public static readonly Version ServerVersion = new(11, 4, 10);

    public const string CharSet = "utf8mb4";
    public const string Collation = "utf8mb4_unicode_ci";

    private readonly ICurrentUser? _currentUser;

    /// <summary>
    /// The current user becomes the scalars the global query filter compares against: EF Core can
    /// translate a property of the context, not a call to a service (design M0 section 3.5). They
    /// are read when a query runs rather than when the context is built, because a context can
    /// well be built before the cookie has been validated. A context with no user at all, as a
    /// background job or a design time tool has, sees what an anonymous visitor sees.
    /// </summary>
    public HubDbContext(DbContextOptions<HubDbContext> options, ICurrentUser? currentUser = null)
        : base(options) => _currentUser = currentUser;

    /// <summary>Director, web team and super administrators read every row, whoever owns it.</summary>
    public bool SeesEveryDepartment => _currentUser is { IsSuperadmin: true } or { HasAllDepartments: true };

    /// <summary>Whether rows restricted to members are readable.</summary>
    public bool SeesMemberRows => _currentUser is { IsAuthenticated: true };

    /// <summary>Whether rows restricted to the staff are readable.</summary>
    public bool SeesStaffRows => _currentUser is { IsStaff: true };

    /// <summary>The departments whose own rows are readable.</summary>
    public List<Department> VisibleDepartments => _currentUser is null ? [] : [.. _currentUser.Departments];

    public DbSet<HubUser> Users => Set<HubUser>();
    public DbSet<UserStaffPosition> UserStaffPositions => Set<UserStaffPosition>();
    public DbSet<UserGrant> UserGrants => Set<UserGrant>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<DivisionSetting> DivisionSettings => Set<DivisionSetting>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<JobLogEntry> JobsLog => Set<JobLogEntry>();

    public DbSet<IvaoCenter> IvaoCenters => Set<IvaoCenter>();
    public DbSet<IvaoAirport> IvaoAirports => Set<IvaoAirport>();

    public DbSet<ContentEntry> Contents => Set<ContentEntry>();
    public DbSet<ContentVersion> ContentVersions => Set<ContentVersion>();
    public DbSet<Link> Links => Set<Link>();
    public DbSet<SearchIndexEntry> SearchIndex => Set<SearchIndexEntry>();
    public DbSet<CalendarEntry> CalendarEntries => Set<CalendarEntry>();
    public DbSet<AwardSignal> AwardSignals => Set<AwardSignal>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Translated fields: one JSON column per row, one converter for the whole model.
        configurationBuilder.Properties<Localized<string>>()
            .HaveConversion<LocalizedConverter<string>, LocalizedComparer<string>>()
            .HaveColumnType("json");
        configurationBuilder.Properties<Localized<JsonNode>>()
            .HaveConversion<LocalizedConverter<JsonNode>, LocalizedComparer<JsonNode>>()
            .HaveColumnType("json");

        // Enums are stored as text: a column that can be read without the code next to it, and a
        // value that never shifts when somebody reorders the enum.
        configurationBuilder.Properties<Department>().HaveConversion<string>().HaveMaxLength(4);
        configurationBuilder.Properties<Visibility>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<PublishStatus>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<StaffLevel>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<ContentKind>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<GrantKind>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<GrantEffect>().HaveConversion<string>().HaveMaxLength(8);
        configurationBuilder.Properties<AwardSignalStatus>().HaveConversion<string>().HaveMaxLength(16);

        // Title -> title_i18n, on top of the snake case convention. One place decides column names.
        configurationBuilder.Conventions.Add(_ => new LocalizedColumnConvention());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasCharSet(CharSet).UseCollation(Collation);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HubDbContext).Assembly);

        // How to read one language out of a translated column in SQL. It maps a method to a
        // MariaDB function: no table, no column, and therefore no migration.
        LocalizedQuery.Register(modelBuilder);

        // Who may read what is decided here, for every entity that has an owner and a visibility,
        // and never again in an endpoint.
        VisibilityQueryFilter.ApplyToModel(modelBuilder, this);
    }
}
