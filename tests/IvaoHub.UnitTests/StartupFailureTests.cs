using IvaoHub.Core.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The reason a start failed, in <c>diagnostics/startup-error.txt</c> (note 2026-09-27-l-avvio-da-qualunque-cartella):
/// what it says, and above all what it never says.
/// </summary>
[Collection(ProcessEnvironmentCollection.Name)]
public sealed class StartupFailureTests : IDisposable
{
    private const string ConnectionString = "Server=db.example.org;Database=hub;User ID=hub;Password=correct-horse-battery";
    private const string ClientSecret = "s3cr3t-of-the-oauth-client";
    private const string SmtpPassword = "mail-password-42";

    private static readonly BuildInfo Build = new("0.2.1", "0123456789abcdef0123456789abcdef01234567", DateTime.UnixEpoch, ".NET 10.0.0");
    private static readonly DateTime FailedAt = new(2026, 9, 27, 23, 15, 0, DateTimeKind.Utc);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ivaohub-failure-{Guid.NewGuid():N}");
    private readonly HubPaths _paths;

    public StartupFailureTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "config"));
        File.WriteAllText(Path.Combine(_root, "config", "division.json"), "{}");
        _paths = HubPaths.Resolve(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void TheReportSaysWhichBuildWhereAndWhy()
    {
        var report = Report(Thrown(new InvalidOperationException(
            "The division file is missing.", new FileNotFoundException("division.json"))), secrets: []);

        Assert.StartsWith("THE APPLICATION DID NOT START.", report, StringComparison.Ordinal);
        Assert.Contains("failed at     2026-09-27T23:15:00.0000000Z", report, StringComparison.Ordinal);
        Assert.Contains("version       0.2.1", report, StringComparison.Ordinal);
        Assert.Contains($"commit        {Build.Commit}", report, StringComparison.Ordinal);
        Assert.Contains("environment   Production", report, StringComparison.Ordinal);
        Assert.Contains($"root          {_root} (found from the content root)", report, StringComparison.Ordinal);
        Assert.Contains("working dir   /somewhere/else", report, StringComparison.Ordinal);
        Assert.Contains("application   /srv/webapp", report, StringComparison.Ordinal);
        Assert.Contains(
            "reason        System.InvalidOperationException: The division file is missing.",
            report,
            StringComparison.Ordinal);

        // The innermost exception, which is often the one that says what happened, and the stack, which is where the
        // line that failed is.
        Assert.Contains("cause         System.IO.FileNotFoundException: division.json", report, StringComparison.Ordinal);
        Assert.Contains(nameof(Thrown), report, StringComparison.Ordinal);
    }

    [Fact]
    public void NoSecretReachesTheFileEvenWhenTheExceptionQuotesIt()
    {
        // The installation's secrets: a file under secrets/ and a password that arrived from the environment.
        Directory.CreateDirectory(_paths.Secrets);
        File.WriteAllText(
            Path.Combine(_paths.Secrets, "installation.json"),
            $$"""{ "Ivao": { "ClientSecret": "{{ClientSecret}}" }, "Installation": { "Preview": true } }""");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["Smtp:Password"] = SmtpPassword,
                ["AllowedHosts"] = "hub.example.org",
            })
            .Build();

        var exception = Thrown(new InvalidOperationException(
            $"Could not open '{ConnectionString}' with '{ClientSecret}' nor '{SmtpPassword}' for hub.example.org."));

        var report = Report(exception, StartupFailure.SecretValues(_paths, configuration));

        Assert.DoesNotContain("correct-horse-battery", report, StringComparison.Ordinal);
        Assert.DoesNotContain("db.example.org", report, StringComparison.Ordinal);
        Assert.DoesNotContain(ClientSecret, report, StringComparison.Ordinal);
        Assert.DoesNotContain(SmtpPassword, report, StringComparison.Ordinal);
        Assert.Contains($"Could not open '{StartupFailure.Redacted}'", report, StringComparison.Ordinal);

        // With no inner exception the reason is the cause, and it is not said twice.
        Assert.DoesNotContain("cause ", report, StringComparison.Ordinal);

        // A value that is not a secret is left as it is, and a short one is never replaced inside other words.
        Assert.Contains("for hub.example.org.", report, StringComparison.Ordinal);
        Assert.Contains("THE APPLICATION DID NOT START.", report, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnreadableSecretsFileIsTheReasonAndNotASecondFailure()
    {
        Directory.CreateDirectory(_paths.Secrets);
        File.WriteAllText(Path.Combine(_paths.Secrets, "broken.json"), "{ \"ConnectionStrings\": ");

        Assert.Empty(StartupFailure.SecretValues(_paths, configuration: null));
    }

    [Fact]
    public void TheFileIsWrittenUnderDiagnosticsAndTheNextStartThatSucceedsRemovesIt()
    {
        // Nothing to remove yet, not even the folder: a first start that succeeds.
        StartupFailure.Clear(_paths);

        var file = StartupFailure.Write(_paths, "the reason");

        Assert.Equal(Path.Combine(_root, "diagnostics", StartupFailure.FileName), file);
        Assert.Equal("the reason", File.ReadAllText(file!));

        StartupFailure.Clear(_paths);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void AFileThatCannotBeWrittenIsNotASecondFailure()
    {
        // diagnostics/ is taken by a file: the folder cannot be created.
        File.WriteAllText(_paths.Diagnostics, "in the way");

        Assert.Null(StartupFailure.Write(_paths, "the reason"));
    }

    private string Report(Exception exception, IEnumerable<string> secrets) => StartupFailure.Report(
        _paths, Build, "Production", exception, FailedAt, "/somewhere/else", "/srv/webapp", secrets);

    /// <summary>An exception with a stack, as the one a failed start leaves.</summary>
    private static Exception Thrown(Exception exception)
    {
        try
        {
            throw exception;
        }
        catch (Exception thrown)
        {
            return thrown;
        }
    }
}
