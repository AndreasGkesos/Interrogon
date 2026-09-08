namespace Interrogon.App.Logging;

/// <summary>Resolves the app's log folder location. Single source of truth for the path.</summary>
public static class LogPaths
{
    public static string LogFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Interrogon",
        "Logs");

    public static string CurrentLogFile => Path.Combine(LogFolder, $"interrogon-{DateTime.Now:yyyyMMdd}.log");
}
