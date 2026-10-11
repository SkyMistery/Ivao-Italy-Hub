using IvaoHub.Core.Data;
using IvaoHub.Core.Privacy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of the audit of a row gone before its person's erasure that need no database (M4, E10n, note
/// 2026-10-11-i-dati-di-una-persona-nell-audit): which columns a mark declares, and what emptying them changes in the JSON of an
/// audit row.
/// </summary>
public sealed class ErasedColumnsTests
{
    [Fact]
    public void TheMarkedPropertiesAreEmptiedAndNothingElse()
    {
        const string json = """{"Id":7,"Callsign":"ABC123","OtherIcao":"XXAA","BookerVid":761100,"SlotId":3}""";

        Assert.Equal(
            """{"Id":7,"Callsign":null,"OtherIcao":null,"BookerVid":761100,"SlotId":3}""",
            AuditRedaction.Empty(json, ["Callsign", "OtherIcao", "UnflownExcusedNote"]));
    }

    [Fact]
    public void AnAuditRowWithNothingToEmptyIsTheSameText()
    {
        // An update that did not change a marked column, a column already empty, the names of an autosave, no data at all.
        const string other = """{"SlotId":4}""";
        const string empty = """{"Callsign":null}""";
        const string names = """["Callsign","SlotId"]""";

        Assert.Same(other, AuditRedaction.Empty(other, ["Callsign"]));
        Assert.Same(empty, AuditRedaction.Empty(empty, ["Callsign"]));
        Assert.Same(names, AuditRedaction.Empty(names, ["Callsign"]));
        Assert.Null(AuditRedaction.Empty(null, ["Callsign"]));
    }

    [Fact]
    public void AMarkIsReadWithThePersonItBelongsTo()
    {
        using var context = new ProbeContext();
        var groups = PersonColumns.ErasedWith(context.Model.FindEntityType(typeof(ProbeBooking))!);

        var group = Assert.Single(groups);
        Assert.Equal(nameof(ProbeBooking.BookerVid), group.Person.Name);
        Assert.Equal([nameof(ProbeBooking.Callsign), nameof(ProbeBooking.Note)], group.Columns.Select(column => column.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AMarkThatNamesNoPersonColumnIsRefused()
    {
        using var context = new ProbeContext();

        var refused = Assert.Throws<InvalidOperationException>(
            () => PersonColumns.ErasedWith(context.Model.FindEntityType(typeof(ProbeMistake))!));
        Assert.Contains("ProbeMistake.Callsign", refused.Message, StringComparison.Ordinal);
    }

    private sealed class ProbeBooking
    {
        public long Id { get; set; }

        public int BookerVid { get; set; }

        [ErasedWith(nameof(BookerVid))]
        public string? Callsign { get; set; }

        [ErasedWith(nameof(BookerVid))]
        public string? Note { get; set; }

        public int? UnflownExcusedBy { get; set; }

        public long SlotId { get; set; }
    }

    private sealed class ProbeMistake
    {
        public long Id { get; set; }

        public long SlotId { get; set; }

        // The slot is not a person.
        [ErasedWith(nameof(SlotId))]
        public string? Callsign { get; set; }
    }

    // A context that is never opened: only its model is read.
    private sealed class ProbeContext() : DbContext(new DbContextOptionsBuilder<ProbeContext>()
        .UseMySql("Server=localhost;Database=none;User ID=none;Password=none", new MariaDbServerVersion(HubDbContext.ServerVersion))
        .Options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProbeBooking>();
            modelBuilder.Entity<ProbeMistake>();
        }
    }
}
