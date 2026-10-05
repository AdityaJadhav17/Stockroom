using System.Diagnostics;
using System.Text;

namespace Stockroom.E2ETests;

// Runs the Release or Debug build of Stockroom.Web as a child process for one test. The process gets its own
// temporary directory, SQLite file, generated passwords, and an operating-system-assigned port. Disposal stops
// that process and deletes only the directory created here.
public sealed class StockroomApp : IAsyncDisposable
{
    public const string Member1 = "member1@stockroom.test";
    public const string Member2 = "member2@stockroom.test";
    public const string Manager = "manager@stockroom.test";

    private const string ListeningPrefix = "Now listening on: ";
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("stockroom-e2e-");
    private readonly string memberPassword = NewPassword();
    private readonly string managerPassword = NewPassword();
    private readonly StringBuilder log = new();
    private Process? server;

    private StockroomApp()
    {
    }

    public Uri BaseUrl { get; private set; } = null!;

    public string DirectoryPath => directory.FullName;

    public string ServerLog
    {
        get
        {
            lock (log)
            {
                return log.ToString();
            }
        }
    }

    private string DatabasePath => Path.Combine(directory.FullName, "stockroom-e2e.db");

    public string PasswordFor(string email) => email == Manager ? managerPassword : memberPassword;

    public static async Task<StockroomApp> StartAsync(CancellationToken cancellationToken)
    {
        var app = new StockroomApp();
        try
        {
            await app.SeedAsync(cancellationToken);
            await app.StartServerAsync(cancellationToken);
            return app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    // Kills the server without a graceful shutdown and starts a new process on the same database (T-13).
    // The new process listens on a new port, so callers open new browser contexts afterwards.
    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        server!.Kill(entireProcessTree: true);
        await server.WaitForExitAsync(cancellationToken);
        server.Dispose();
        server = null;
        await StartServerAsync(cancellationToken);
    }

    // The real Development seed command creates the documented demo dataset in the fixture's database.
    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        using var seed = Start("seed");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(StartupTimeout);
        try
        {
            await seed.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            seed.Kill(entireProcessTree: true);
            throw;
        }
        if (seed.ExitCode != 0)
        {
            throw new InvalidOperationException($"The seed command exited with code {seed.ExitCode}.\n{ServerLog}");
        }
    }

    private async Task StartServerAsync(CancellationToken cancellationToken)
    {
        var listening = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        server = Start(null, line =>
        {
            var index = line.IndexOf(ListeningPrefix, StringComparison.Ordinal);
            if (index >= 0)
            {
                listening.TrySetResult(new Uri(line[(index + ListeningPrefix.Length)..].Trim()));
            }
        });
        server.Exited += (_, _) => listening.TrySetException(
            new InvalidOperationException($"The application exited before it started listening.\n{ServerLog}"));

        BaseUrl = await listening.Task.WaitAsync(StartupTimeout, cancellationToken);

        // Ready once the login page answers, which needs routing, Razor Pages, and static assets.
        using var http = new HttpClient { BaseAddress = BaseUrl };
        var deadline = DateTime.UtcNow + StartupTimeout;
        while (true)
        {
            try
            {
                using var response = await http.GetAsync("/Account/Login", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException) when (DateTime.UtcNow < deadline)
            {
            }
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"The application did not answer /Account/Login.\n{ServerLog}");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private Process Start(string? command, Action<string>? onOutput = null)
    {
        var (assembly, contentRoot) = LocateWebBuild();
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = contentRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add(assembly);
        if (command is not null)
        {
            info.ArgumentList.Add(command);
        }
        // Environment variables take precedence over appsettings and the developer's user secrets.
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        info.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        info.Environment["ConnectionStrings__Stockroom"] = $"Data Source={DatabasePath}";
        info.Environment["Seed__MemberPassword"] = memberPassword;
        info.Environment["Seed__ManagerPassword"] = managerPassword;
        // On Windows the host also writes warnings and errors to the Event Log. Tests turn that provider off so
        // they leave nothing in the machine's Application log; the server log still captures the console output.
        info.Environment["Logging__EventLog__LogLevel__Default"] = "None";

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        DataReceivedEventHandler record = (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }
            lock (log)
            {
                log.AppendLine(e.Data);
            }
            onOutput?.Invoke(e.Data);
        };
        process.OutputDataReceived += record;
        process.ErrorDataReceived += record;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    // The checkout that contains this test assembly's build output.
    public static string RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Stockroom.slnx")))
        {
            root = root.Parent;
        }
        return root?.FullName
            ?? throw new InvalidOperationException("Could not find Stockroom.slnx above the test output directory.");
    }

    // Uses the web project's own build output for the same configuration as this test assembly.
    private static (string Assembly, string ContentRoot) LocateWebBuild()
    {
        var testOutput = new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        var configuration = testOutput.Parent!.Name;
        var contentRoot = Path.Combine(RepositoryRoot(), "src", "Stockroom.Web");
        var assembly = Path.Combine(contentRoot, "bin", configuration, testOutput.Name, "Stockroom.Web.dll");
        if (!File.Exists(assembly))
        {
            throw new FileNotFoundException(
                $"Build the web application first: dotnet build Stockroom.slnx --configuration {configuration}", assembly);
        }
        return (assembly, contentRoot);
    }

    private static string NewPassword() => $"E2e-{Guid.NewGuid():N}-Aa1!";

    public async ValueTask DisposeAsync()
    {
        if (server is not null)
        {
            if (!server.HasExited)
            {
                server.Kill(entireProcessTree: true);
            }
            await server.WaitForExitAsync();
            server.Dispose();
        }

        // Windows can release a killed process's file handles shortly after the exit is reported.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                directory.Delete(recursive: true);
                return;
            }
            catch (IOException) when (attempt < 20)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
        }
    }
}
