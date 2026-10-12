using System.ComponentModel.DataAnnotations.Schema;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of the audit of a row gone before its person's erasure that need no database (M4, E10n, note
/// 2026-10-11-i-dati-di-una-persona-nell-audit): which columns a mark declares and which marks are refused, what emptying them
/// changes in the JSON of an audit row, and which copies of a row were written while it was the person's.
/// </summary>
public sealed class ErasedColumnsTests
{
    private const int Vid = 761100;
    private static readonly string[] Data = ["Callsign"];

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

        Assert.Same(other, AuditRedaction.Empty(other, Data));
        Assert.Same(empty, AuditRedaction.Empty(empty, Data));
        Assert.Same(names, AuditRedaction.Empty(names, Data));
        Assert.Null(AuditRedaction.Empty(null, Data));
    }

    [Fact]
    public void ARowThatChangedHandsLosesOnlyTheCopiesWrittenWhileItWasThePersons()
    {
        var rows = new[]
        {
            Copy("created", null, """{"Id":1,"BookerVid":761100,"Callsign":"AAA111"}"""),
            Copy("updated", """{"Callsign":"AAA111"}""", """{"Callsign":"AAA222"}"""),
            Copy("updated", """{"BookerVid":761100,"Callsign":"AAA222"}""", """{"BookerVid":780096,"Callsign":"BBB111"}"""),
            Copy("updated", """{"Callsign":"BBB111"}""", """{"Callsign":"BBB222"}"""),
            Copy("deleted", """{"Id":1,"BookerVid":780096,"Callsign":"BBB222"}""", null),
        };

        AuditRedaction.EmptyWhileHeld(rows, "BookerVid", Data, Vid);

        Assert.Equal(
            [
                (null, """{"Id":1,"BookerVid":761100,"Callsign":null}"""),
                ("""{"Callsign":null}""", """{"Callsign":null}"""),
                ("""{"BookerVid":761100,"Callsign":null}""", """{"BookerVid":780096,"Callsign":"BBB111"}"""),
                ("""{"Callsign":"BBB111"}""", """{"Callsign":"BBB222"}"""),
                ("""{"Id":1,"BookerVid":780096,"Callsign":"BBB222"}""", null),
            ],
            rows.Select(row => (row.BeforeJson, row.AfterJson)));
    }

    [Fact]
    public void AHistoryBegunBeforeTheAuditIsReadFromTheFirstCopyThatSaysWhoseItWas()
    {
        // No "created": the table was audited after the row was written. The first copy that names the person column says.
        var theirs = new[]
        {
            Copy("updated", """{"Callsign":"AAA111"}""", """{"Callsign":"AAA222"}"""),
            Copy("deleted", """{"Id":2,"BookerVid":761100,"Callsign":"AAA222"}""", null),
        };
        var others = new[]
        {
            Copy("updated", """{"Callsign":"BBB111"}""", """{"Callsign":"BBB222"}"""),
            Copy("deleted", """{"Id":3,"BookerVid":780096,"Callsign":"BBB222"}""", null),
        };

        AuditRedaction.EmptyWhileHeld(theirs, "BookerVid", Data, Vid);
        AuditRedaction.EmptyWhileHeld(others, "BookerVid", Data, Vid);

        Assert.All(theirs, row => Assert.DoesNotContain("AAA", $"{row.BeforeJson}{row.AfterJson}", StringComparison.Ordinal));
        Assert.Equal("""{"Callsign":"BBB111"}""", others[0].BeforeJson);
        Assert.Equal("""{"Id":3,"BookerVid":780096,"Callsign":"BBB222"}""", others[1].BeforeJson);
    }

    [Fact]
    public void TheVidInAnotherPropertyIsNotThePerson()
    {
        // The person wrote the row, as staff, and their number is also a word of it: the row is about somebody else.
        var rows = new[]
        {
            Copy("created", null, """{"Id":4,"BookerVid":780096,"CreatedBy":761100,"Callsign":"761100"}"""),
            Copy("deleted", """{"Id":4,"BookerVid":780096,"CreatedBy":761100,"Callsign":"761100"}""", null),
        };

        AuditRedaction.EmptyWhileHeld(rows, "BookerVid", Data, Vid);

        Assert.All(rows, row => Assert.Contains("\"Callsign\":\"761100\"", $"{row.BeforeJson}{row.AfterJson}", StringComparison.Ordinal));
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

    [Theory]
    [InlineData(typeof(ProbeMistake), "ProbeMistake.Callsign", "'SlotId' is not")]
    [InlineData(typeof(ProbeNotAudited), "ProbeNotAudited.Callsign", "not [Audited]")]
    [InlineData(typeof(ProbeTwoColumnKey), "ProbeTwoColumnKey.Callsign", "not one column")]
    [InlineData(typeof(ProbeUnmapped), "ProbeUnmapped.Remark", "does not map it")]
    public void AMarkTheErasureCouldNotHonourIsRefused(Type probe, string where, string why)
    {
        using var context = new ProbeContext();

        var refused = Assert.Throws<InvalidOperationException>(() => PersonColumns.ErasedWith(context.Model.FindEntityType(probe)!));
        Assert.Contains(where, refused.Message, StringComparison.Ordinal);
        Assert.Contains(why, refused.Message, StringComparison.Ordinal);

        // And the hub would not start with it.
        Assert.Throws<InvalidOperationException>(() => PersonColumns.VerifyErasedWith(context.Model));
    }

    private static AuditLogEntry Copy(string action, string? before, string? after) =>
        new() { Action = action, Entity = "probe", EntityId = "1", BeforeJson = before, AfterJson = after };

    [Audited]
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

    [Audited]
    private sealed class ProbeMistake
    {
        public long Id { get; set; }

        public long SlotId { get; set; }

        // The slot is not a person.
        [ErasedWith(nameof(SlotId))]
        public string? Callsign { get; set; }
    }

    private sealed class ProbeNotAudited
    {
        public long Id { get; set; }

        public int BookerVid { get; set; }

        [ErasedWith(nameof(BookerVid))]
        public string? Callsign { get; set; }
    }

    [Audited]
    private sealed class ProbeTwoColumnKey
    {
        public long EventId { get; set; }

        public int BookerVid { get; set; }

        [ErasedWith(nameof(BookerVid))]
        public string? Callsign { get; set; }
    }

    [Audited]
    private sealed class ProbeUnmapped
    {
        public long Id { get; set; }

        public int BookerVid { get; set; }

        [NotMapped]
        [ErasedWith(nameof(BookerVid))]
        public string? Remark { get; set; }
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
            modelBuilder.Entity<ProbeNotAudited>();
            modelBuilder.Entity<ProbeTwoColumnKey>().HasKey(row => new { row.EventId, row.BookerVid });
            modelBuilder.Entity<ProbeUnmapped>();
        }
    }
}
