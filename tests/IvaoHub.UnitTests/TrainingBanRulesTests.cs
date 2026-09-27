using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Bans;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of the bans of A10a with no database (design M3 §2.9): what a ban given by the staff may be, and a ban as its row says it
/// to the list, the form and the trainee's path. Whether a ban holds is A6a's (<c>TraineeBan.Holds</c>, in the rules of the request).
/// </summary>
public sealed class TrainingBanRulesTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ABanNamesAMemberAReasonAndAnEndStillToCome()
    {
        var validator = new TraineeBanWriteDtoValidator(new StubClock(Now));

        Assert.True(validator.Validate(new TraineeBanWriteDto(4242, "trn-test", null)).IsValid);
        Assert.True(validator.Validate(new TraineeBanWriteDto(4242, "trn-test", Now.AddMinutes(1))).IsValid);

        Assert.Equal(["errors.required"], Messages(new TraineeBanWriteDto(0, "trn-test", null), nameof(TraineeBanWriteDto.Vid)));
        Assert.Equal(["errors.required"], Messages(new TraineeBanWriteDto(4242, "  ", null), nameof(TraineeBanWriteDto.Reason)));
        Assert.Equal(["errors.text.tooLong"], Messages(new TraineeBanWriteDto(4242, new string('x', Training.MaxTextLength + 1), null), nameof(TraineeBanWriteDto.Reason)));

        // An end already gone by, or now, says nothing: the ban would never hold.
        Assert.Equal([TrainingBans.EndsInThePast], Messages(new TraineeBanWriteDto(4242, "trn-test", Now), nameof(TraineeBanWriteDto.EndsAt)));
        Assert.Equal([TrainingBans.EndsInThePast], Messages(new TraineeBanWriteDto(4242, "trn-test", Now.AddDays(-1)), nameof(TraineeBanWriteDto.EndsAt)));

        IEnumerable<string> Messages(TraineeBanWriteDto ban, string property) =>
            validator.Validate(ban).Errors.Where(error => error.PropertyName == property).Select(error => error.ErrorMessage);
    }

    [Fact]
    public void ABanAsItsRowSaysIt()
    {
        var names = new Dictionary<int, string> { [4242] = "Test Trainee", [7] = "Test Coordinator" };
        var ban = new TraineeBan { Id = 9, Vid = 4242, Reason = "trn-test", CreatedAt = Now.AddDays(-1), CreatedBy = 7, EndsAt = Now.AddDays(3) };

        var row = TrainingBans.Row(ban, names, Now);
        Assert.Equal((4242, "Test Trainee"), (row.Trainee.Vid, row.Trainee.Name));
        Assert.Equal(7, row.GivenBy?.Vid);
        Assert.True(row.Holds);
        Assert.Null(row.LiftedBy);

        // Lifted: who and when, and it holds no more; the one who lifted it the hub may not know by name.
        ban.LiftedBy = 8;
        ban.LiftedAt = Now.AddHours(-1);
        row = TrainingBans.Row(ban, names, Now);
        Assert.False(row.Holds);
        Assert.Equal((8, (string?)null), (row.LiftedBy!.Vid, row.LiftedBy.Name));

        // Over: it holds no more either, and nobody lifted it.
        var over = TrainingBans.Row(new TraineeBan { Vid = 4242, Reason = "trn-test", CreatedAt = Now.AddDays(-9), CreatedBy = 7, EndsAt = Now.AddDays(-2) }, names, Now);
        Assert.False(over.Holds);
        Assert.Null(over.LiftedBy);

        // Written by the installation itself: nobody gave it.
        Assert.Null(TrainingBans.Row(new TraineeBan { Vid = 4242, Reason = "trn-test", CreatedAt = Now }, names, Now).GivenBy);
    }

    private sealed class StubClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }
}
