using System.Reflection;
using IvaoHub.Core.Services;

namespace IvaoHub.Web;

/// <summary>
/// Turns an exception that stops the process before the application has started into
/// <c>diagnostics/startup-error.txt</c> (<see cref="StartupFailure"/>, note
/// 2026-09-27-l-avvio-da-qualunque-cartella). It listens for the exception leaving the program rather
/// than wrapping the program in a <c>try</c>: the runtime still prints it and still exits with an
/// error, exactly as before, and every failure is covered, the ones inside
/// <c>WebApplication.CreateBuilder</c> included.
/// </summary>
internal sealed class StartupFailureWatch
{
    private readonly HubPaths _paths;
    private IConfiguration? _configuration;
    private string _environment;
    private volatile bool _started;

    private StartupFailureWatch(HubPaths paths)
    {
        _paths = paths;
        _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environments.Production;
    }

    /// <summary>
    /// Armed only when this process is the hub itself. Under the test host and under the tool that
    /// writes the OpenAPI document the entry point is somebody else's, the exceptions are theirs to
    /// report, and a test that proves a refused start must not leave a file behind.
    /// </summary>
    public static StartupFailureWatch? Arm(HubPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (Assembly.GetEntryAssembly() != typeof(StartupFailureWatch).Assembly)
        {
            return null;
        }

        var watch = new StartupFailureWatch(paths);
        AppDomain.CurrentDomain.UnhandledException += watch.OnUnhandledException;
        return watch;
    }

    /// <summary>The configuration once it exists, for the values the report must not quote, and the real environment.</summary>
    public void Attach(IConfiguration configuration, string environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    /// <summary>The start succeeded: the report of an earlier failure goes, and a later crash is not a failed start.</summary>
    public void Started()
    {
        _started = true;
        StartupFailure.Clear(_paths);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs arguments)
    {
        if (_started || arguments.ExceptionObject is not Exception exception)
        {
            return;
        }

        var report = StartupFailure.Report(
            _paths,
            BuildInfo.FromAssembly(typeof(StartupFailureWatch).Assembly),
            _environment,
            exception,
            DateTime.UtcNow,
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            StartupFailure.SecretValues(_paths, _configuration));

        if (StartupFailure.Write(_paths, report) is { } file)
        {
            Console.Error.WriteLine($"The reason is also in {file}.");
        }
    }
}
