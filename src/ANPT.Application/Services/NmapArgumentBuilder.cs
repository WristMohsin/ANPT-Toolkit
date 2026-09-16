using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using ANPT.Domain.Entities;

namespace ANPT.Application.Services;

/// <summary>
/// Allow-listed mapping from ScanProfile flags to discrete Nmap arguments.
/// Never accepts raw user-supplied switches.
/// </summary>
public static class NmapArgumentBuilder
{
    private static readonly Regex SafeTargetPattern = new(
        @"^([a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*|" +
        @"(\d{1,3}\.){3}\d{1,3}(/\d{1,2})?|" +
        @"([0-9a-fA-F]{0,4}:){2,7}[0-9a-fA-F]{0,4})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryBuild(
        string targetAddress,
        ScanProfile profile,
        string? xmlOutputPath,
        out IReadOnlyList<string> arguments,
        out string? error)
    {
        arguments = Array.Empty<string>();
        error = null;

        if (string.IsNullOrWhiteSpace(targetAddress))
        {
            error = "Target address is empty.";
            return false;
        }

        var target = targetAddress.Trim();
        if (target.Length > 100)
        {
            error = "Target address is too long.";
            return false;
        }

        if (target.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '|', '&', ';', '$', '`', '"', '\'', '<', '>', '(', ')', '{', '}', '\\' }) >= 0)
        {
            error = "Target address contains disallowed characters.";
            return false;
        }

        if (!IsAcceptableTarget(target))
        {
            error = "Target address format is not accepted for execution.";
            return false;
        }

        if (profile is null)
        {
            error = "Scan profile is required.";
            return false;
        }

        var args = new List<string>();
        args.Add("-n");
        args.Add("-T3");

        if (profile.IncludeHostDiscovery && !profile.IncludePortScanning && !profile.IncludeServiceEnumeration)
        {
            args.Add("-sn");
        }
        else
        {
            if (profile.IncludePortScanning || profile.IncludeServiceEnumeration)
            {
                args.Add("-sT");
                args.Add("--top-ports");
                args.Add("100");
            }
            else if (profile.IncludeHostDiscovery)
            {
                args.Add("-sn");
            }

            if (profile.IncludeServiceEnumeration)
            {
                args.Add("-sV");
                args.Add("--version-light");
            }
        }

        // Vulnerability / correlation flags are NOT mapped to Nmap in Phase 5A.

        if (!string.IsNullOrWhiteSpace(xmlOutputPath))
        {
            args.Add("-oX");
            args.Add(xmlOutputPath);
        }

        args.Add(target);
        arguments = args;
        return true;
    }

    public static bool IsAcceptableTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return false;

        if (IPAddress.TryParse(target, out var ip))
            return ip.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6;

        var slash = target.IndexOf('/');
        if (slash > 0)
        {
            var basePart = target[..slash];
            var prefix = target[(slash + 1)..];
            if (!int.TryParse(prefix, out var bits) || bits < 0 || bits > 128)
                return false;
            return IPAddress.TryParse(basePart, out _);
        }

        return SafeTargetPattern.IsMatch(target);
    }
}
