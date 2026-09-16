using System.Diagnostics;
using System.Text;
using ANPT.Application.Interfaces;
using ANPT.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace ANPT.Infrastructure.Scanning;

/// <summary>
/// Windows-oriented safe Nmap launcher. No shell, no cmd, ArgumentList only.
/// </summary>
public sealed class NmapProcessRunner : INmapProcessRunner
{
    private readonly ILogger<NmapProcessRunner> _logger;
    private readonly string? _configuredPath;

    public NmapProcessRunner(ILogger<NmapProcessRunner> logger)
    {
        _logger = logger;
        _configuredPath = TryReadConfiguredPath();
    }

    public async Task<NmapAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var path = ResolveNmapPath();
        if (path is null)
            return NmapAvailabilityResult.Unavailable(
                "Nmap executable was not found. Install Nmap or set Config/nmap.path to the full path of nmap.exe.");

        if (!IsTrustedNmapFileName(path))
            return NmapAvailabilityResult.Unavailable("Configured path is not a valid Nmap executable name.");

        try
        {
            var psi = CreateStartInfo(path, new[] { "--version" });
            using var process = new Process { StartInfo = psi };
            if (!process.Start())
                return NmapAvailabilityResult.Unavailable("Unable to start Nmap for version check.");

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var reg = cancellationToken.Register(() => TryKill(process));
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var text = stdout.ToString().Trim();
            if (string.IsNullOrEmpty(text))
                text = stderr.ToString().Trim();

            _logger.LogInformation("Nmap available at {Path}", path);
            return NmapAvailabilityResult.Available(path, string.IsNullOrEmpty(text) ? null : text);
        }
        catch (OperationCanceledException)
        {
            return NmapAvailabilityResult.Unavailable("Nmap version check was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nmap version check failed for {Path}", path);
            return NmapAvailabilityResult.Unavailable($"Unable to run Nmap: {ex.Message}");
        }
    }

    public async Task<NmapRunResult> RunAsync(NmapRunRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            return NmapRunResult.StartupFailure("Run request is required.");

        if (string.IsNullOrWhiteSpace(request.ExecutablePath))
            return NmapRunResult.StartupFailure("Nmap executable path is required.");

        if (!IsTrustedNmapFileName(request.ExecutablePath))
            return NmapRunResult.StartupFailure("Executable path must point to nmap or nmap.exe.");

        if (!File.Exists(request.ExecutablePath))
            return NmapRunResult.StartupFailure("Nmap executable file does not exist.");

        if (request.Arguments is null || request.Arguments.Count == 0)
            return NmapRunResult.StartupFailure("Nmap arguments are required.");

        foreach (var arg in request.Arguments)
        {
            if (arg is null)
                return NmapRunResult.StartupFailure("Null argument is not allowed.");
        }

        string? xmlPath = request.XmlOutputPath;
        if (!string.IsNullOrWhiteSpace(xmlPath))
        {
            if (!IsPathUnderAllowedRoot(xmlPath))
                return NmapRunResult.StartupFailure("XML output path is outside the allowed application directory.");
            Directory.CreateDirectory(Path.GetDirectoryName(xmlPath)!);
        }

        var workDir = request.WorkingDirectory;
        if (!string.IsNullOrWhiteSpace(workDir))
        {
            if (!IsPathUnderAllowedRoot(workDir))
                return NmapRunResult.StartupFailure("Working directory is outside the allowed application directory.");
            Directory.CreateDirectory(workDir);
        }
        else
        {
            workDir = AppPaths.ScanOutputDirectory;
            Directory.CreateDirectory(workDir);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var psi = CreateStartInfo(request.ExecutablePath, request.Arguments);
            psi.WorkingDirectory = workDir;

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

            _logger.LogInformation("Starting Nmap process. ArgCount={Count}", request.Arguments.Count);

            if (!process.Start())
                return NmapRunResult.StartupFailure("Process.Start returned false.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var cancelled = false;
            using (cancellationToken.Register(() =>
                   {
                       cancelled = true;
                       TryKill(process);
                   }))
            {
                try
                {
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    TryKill(process);
                    try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); }
                    catch { /* ignore */ }
                }
            }

            sw.Stop();
            try { process.WaitForExit(2000); } catch { /* ignore */ }

            var exitCode = process.HasExited ? process.ExitCode : -1;
            _logger.LogInformation(
                "Nmap process exited. ExitCode={ExitCode} Cancelled={Cancelled} DurationMs={Ms}",
                exitCode, cancelled, sw.ElapsedMilliseconds);

            return NmapRunResult.FromProcess(
                exitCode, stdout.ToString(), stderr.ToString(), sw.Elapsed, cancelled, xmlPath);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return NmapRunResult.FromProcess(-1, string.Empty, string.Empty, sw.Elapsed, cancelled: true, xmlPath);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to run Nmap process");
            return NmapRunResult.StartupFailure($"Unable to start Nmap process: {ex.Message}");
        }
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, IReadOnlyList<string> arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false
        };

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        return psi;
    }

    private string? ResolveNmapPath()
    {
        if (!string.IsNullOrWhiteSpace(_configuredPath) && File.Exists(_configuredPath) && IsTrustedNmapFileName(_configuredPath))
            return Path.GetFullPath(_configuredPath);

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nmap", "nmap.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Nmap", "nmap.exe"),
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        var fromPath = FindOnPath("nmap.exe") ?? FindOnPath("nmap");
        if (fromPath is not null && IsTrustedNmapFileName(fromPath))
            return fromPath;

        return null;
    }

    private static string? TryReadConfiguredPath()
    {
        try
        {
            var configFile = Path.Combine(AppPaths.ConfigDirectory, "nmap.path");
            if (!File.Exists(configFile))
                return null;
            var line = File.ReadAllLines(configFile)
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.Length > 0 && !l.StartsWith('#') && !l.StartsWith(';'));
            return string.IsNullOrWhiteSpace(line) ? null : line.Trim().Trim('"');
        }
        catch
        {
            return null;
        }
    }

    private static string? FindOnPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(dir.Trim(), fileName);
                if (File.Exists(full))
                    return Path.GetFullPath(full);
            }
            catch { /* ignore */ }
        }

        return null;
    }

    private static bool IsTrustedNmapFileName(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals("nmap.exe", StringComparison.OrdinalIgnoreCase)
               || name.Equals("nmap", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPathUnderAllowedRoot(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(AppPaths.BaseDirectory);
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                   || full.StartsWith(Path.GetFullPath(AppPaths.ScanOutputDirectory), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                _logger.LogInformation("Terminating Nmap process due to cancellation");
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill Nmap process");
        }
    }
}
