using System.Diagnostics;
using Interrogon.App.Logging;
using Interrogon.App.ViewModels;
using Interrogon.Core.Interfaces;
using Interrogon.Services.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace Interrogon.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Bootstrapped in the constructor so it's available to log exceptions that occur before
    /// <see cref="Services"/> is built (or if building it fails). <see cref="BuildServiceProvider"/>
    /// wires this same instance into the DI <see cref="ILoggerFactory"/> via AddSerilog, so
    /// application code always logs through the standard ILogger&lt;T&gt; pattern — this field
    /// exists solely for the crash handler's early-failure case, never called directly elsewhere.
    /// </summary>
    private static readonly Serilog.ILogger RootLogger = BuildRootLogger();

    private Window? _window;

    public App()
    {
        InitializeComponent();

        UnhandledException += OnAppUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private static Serilog.ILogger BuildRootLogger()
    {
        Directory.CreateDirectory(LogPaths.LogFolder);
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(LogPaths.LogFolder, "interrogon-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}")
            .CreateLogger();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services = BuildServiceProvider();

        Services.GetRequiredService<ILogger<App>>().LogInformation("App started at {StartedAt}", DateTime.Now);

        _window = new MainWindow();
        _window.Closed += (_, _) => (Services as IDisposable)?.Dispose();
        _window.Activate();
    }

    private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        HandleUnhandledException(e.Exception, "App.UnhandledException");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        HandleUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception ?? new Exception($"Non-exception object thrown: {e.ExceptionObject}");
        HandleUnhandledException(exception, "AppDomain.UnhandledException");
    }

    private void HandleUnhandledException(Exception exception, string source)
    {
        WriteCrashLog(exception, source);
        TryShowCrashDialog();
    }

    private static void WriteCrashLog(Exception exception, string source)
    {
        try
        {
            RootLogger.ForContext<App>().Error(exception, "Unhandled exception via {Source}", source);
        }
        catch
        {
            // Logging must never itself crash the crash handler.
        }
    }

    private void TryShowCrashDialog()
    {
        try
        {
            var window = _window;
            var xamlRoot = window?.Content?.XamlRoot;
            if (window is null || xamlRoot is null)
                return;

            _ = window.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var dialog = new ContentDialog
                    {
                        XamlRoot = xamlRoot,
                        Title = "Something went wrong",
                        Content = "Interrogon encountered an unexpected error. The details have been saved to the log file.",
                        PrimaryButtonText = "Open Log Folder",
                        CloseButtonText = "Close",
                    };

                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        Directory.CreateDirectory(LogPaths.LogFolder);
                        Process.Start("explorer.exe", LogPaths.LogFolder);
                    }
                }
                catch
                {
                    // Never let the crash dialog itself crash the app.
                }
            });
        }
        catch
        {
            // Never let the crash dialog itself crash the app.
        }
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.AddSerilog(RootLogger, dispose: false);
        });

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Interrogon");
        var dbPath = Path.Combine(appDataDir, "interrogon.db");

        services.AddSingleton(_ => new LiteDbContext(dbPath));
        services.AddSingleton<IQuestionRepository, QuestionRepository>();
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<ITagRepository, TagRepository>();
        services.AddSingleton<IRoadmapRepository, RoadmapRepository>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }
}
