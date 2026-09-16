using ANPT.Application.Models.Nmap;
using ANPT.Application.Services;
using ANPT.Domain.Entities;
using ANPT.Infrastructure.Configuration;
using ANPT.Infrastructure.Scanning;
using Microsoft.Extensions.Logging.Abstractions;

namespace ANPT.Tests.Application;

public class NmapXmlResultReaderTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _scanOutput;
    private readonly NmapXmlResultReader _reader;

    public NmapXmlResultReaderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "anpt-5c-" + Guid.NewGuid().ToString("N"));
        _scanOutput = Path.Combine(_tempRoot, "ScanOutput");
        Directory.CreateDirectory(_scanOutput);

        AppPaths.EnsureDirectoriesExist();
        _reader = new NmapXmlResultReader(new NmapXmlParser(), NullLogger<NmapXmlResultReader>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch { /* ignore */ }
    }

    private static string ControlledPath(string fileName)
    {
        AppPaths.EnsureDirectoriesExist();
        return Path.Combine(AppPaths.ScanOutputDirectory, fileName);
    }

    [Fact]
    public async Task Read_ValidFile_Parses()
    {
        var path = ControlledPath($"valid-{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(path, """
            <nmaprun scanner="nmap" version="7.94">
              <host>
                <status state="up"/>
                <address addr="10.0.0.5" addrtype="ipv4"/>
              </host>
            </nmaprun>
            """);
        try
        {
            var result = await _reader.ReadAsync(path);
            Assert.True(result.Succeeded);
            Assert.Equal("nmap", result.Scan!.Scanner);
            Assert.Single(result.Scan.Hosts);
            Assert.Equal("10.0.0.5", result.Scan.Hosts[0].Addresses[0].Address);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Read_MissingFile_Fails()
    {
        var path = ControlledPath($"missing-{Guid.NewGuid():N}.xml");
        var result = await _reader.ReadAsync(path);
        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Read_EmptyFile_Fails()
    {
        var path = ControlledPath($"empty-{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(path, "   ");
        try
        {
            var result = await _reader.ReadAsync(path);
            Assert.False(result.Succeeded);
            Assert.Contains("empty", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Read_MalformedFile_Fails()
    {
        var path = ControlledPath($"bad-{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(path, "<nmaprun><host></nmaprun>");
        try
        {
            var result = await _reader.ReadAsync(path);
            Assert.False(result.Succeeded);
            Assert.NotNull(result.ErrorMessage);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Read_PathOutsideScanOutput_Rejected()
    {
        var outside = Path.Combine(_tempRoot, "evil.xml");
        Directory.CreateDirectory(_tempRoot);
        await File.WriteAllTextAsync(outside, "<nmaprun scanner=\"nmap\"/>");
        var result = await _reader.ReadAsync(outside);
        Assert.False(result.Succeeded);
        Assert.Contains("outside", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Read_NullOrEmptyPath_Fails()
    {
        Assert.False((await _reader.ReadAsync(null!)).Succeeded);
        Assert.False((await _reader.ReadAsync("")).Succeeded);
        Assert.False((await _reader.ReadAsync("  ")).Succeeded);
    }

    [Fact]
    public async Task ReadForScan_UsesOutputFilePath()
    {
        var path = ControlledPath($"scanfile-{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(path, """
            <nmaprun scanner="nmap" version="7.80">
              <runstats><finished elapsed="1.0" exit="success"/></runstats>
            </nmaprun>
            """);
        try
        {
            var scan = new Scan { OutputFilePath = path };
            var result = await _reader.ReadForScanAsync(scan);
            Assert.True(result.Succeeded);
            Assert.Equal("7.80", result.Scan!.Version);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task ReadForScan_MissingOutputPath_Fails()
    {
        var scan = new Scan { OutputFilePath = null };
        var result = await _reader.ReadForScanAsync(scan);
        Assert.False(result.Succeeded);
        Assert.Contains("no output", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadForScan_NullScan_Fails()
    {
        var result = await _reader.ReadForScanAsync(null!);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void IsUnderScanOutput_RejectsTraversal()
    {
        AppPaths.EnsureDirectoriesExist();
        var outside = Path.GetFullPath(Path.Combine(AppPaths.ScanOutputDirectory, "..", "Data", "anpt.db"));
        Assert.False(NmapXmlResultReader.IsUnderScanOutput(outside));
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }
}
