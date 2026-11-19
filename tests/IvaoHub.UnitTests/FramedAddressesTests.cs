using System.Text.Json;
using IvaoHub.Core.Content;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The server's half of the one list of hosts a video or an embed block may frame (#524, note
/// 2026-11-18-l-indirizzo-che-l-hub-non-incorpora). The list is the browser's <c>web/src/blocks/embedHosts.json</c>,
/// compiled in; the rules that read it are written on both sides, so both are held to the same table,
/// <c>web/src/blocks/embedHosts.cases.json</c>: here for "would it be framed?", in <c>embedHosts.test.ts</c> for the
/// frame itself. A case added there is asked of both.
/// </summary>
public sealed class FramedAddressesTests
{
    public static TheoryData<string, bool> Cases()
    {
        var data = new TheoryData<string, bool>();

        using var cases = JsonDocument.Parse(File.ReadAllText(CasesFile()));
        foreach (var entry in cases.RootElement.EnumerateArray())
        {
            data.Add(
                entry.GetProperty("address").GetString()!,
                entry.GetProperty("framed").ValueKind != JsonValueKind.Null);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheServerAnswersWhatTheBrowserDoes(string address, bool framed) =>
        Assert.Equal(framed, FramedAddresses.Frames(address));

    [Fact]
    public void TheTableIsReadAndAsksBothAnswers()
    {
        var cases = Cases().Select(row => (bool)row.Data.Item2).ToList();

        Assert.Contains(true, cases);
        Assert.Contains(false, cases);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoAddressIsNotFramed(string? address) => Assert.False(FramedAddresses.Frames(address));

    private static string CasesFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IvaoHub.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "web", "src", "blocks", "embedHosts.cases.json");
    }
}
