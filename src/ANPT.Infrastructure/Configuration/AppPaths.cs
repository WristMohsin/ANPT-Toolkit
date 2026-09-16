namespace ANPT.Infrastructure.Configuration;

/// <summary>
/// Provides application-relative paths that work on any Windows machine.
/// </summary>
public static class AppPaths
{
    public static readonly string BaseDirectory = AppContext.BaseDirectory;

    public static string DataDirectory => Path.Combine(BaseDirectory, "Data");
    public static string LogsDirectory => Path.Combine(BaseDirectory, "Logs");
    public static string ReportsDirectory => Path.Combine(BaseDirectory, "Reports");
    public static string ConfigDirectory => Path.Combine(BaseDirectory, "Config");
    public static string ScanOutputDirectory => Path.Combine(BaseDirectory, "ScanOutput");

    public static string DatabasePath => Path.Combine(DataDirectory, "anpt.db");
    public static string LogFilePath => Path.Combine(LogsDirectory, "anpt-.log");

    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ReportsDirectory);
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(ScanOutputDirectory);
    }
}
