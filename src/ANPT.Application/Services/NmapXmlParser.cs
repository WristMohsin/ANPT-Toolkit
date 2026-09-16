using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using ANPT.Application.Interfaces;
using ANPT.Application.Models.Nmap;

namespace ANPT.Application.Services;

/// <summary>
/// Secure Nmap XML parser using System.Xml.Linq with external entity/DTD resolution disabled.
/// </summary>
public sealed class NmapXmlParser : INmapXmlParser
{
    public NmapParseResult Parse(string xml)
    {
        if (xml is null)
            return NmapParseResult.Failure("XML content is required.");

        if (string.IsNullOrWhiteSpace(xml))
            return NmapParseResult.Failure("XML content is empty.");

        XDocument doc;
        try
        {
            doc = LoadSecure(xml);
        }
        catch (XmlException ex)
        {
            return NmapParseResult.Failure($"Malformed XML: {ex.Message}");
        }
        catch (Exception ex)
        {
            return NmapParseResult.Failure($"Unable to load XML: {ex.Message}");
        }

        var root = doc.Root;
        if (root is null)
            return NmapParseResult.Failure("XML document has no root element.");

        if (!root.Name.LocalName.Equals("nmaprun", StringComparison.OrdinalIgnoreCase))
            return NmapParseResult.Failure($"Unexpected root element '{root.Name.LocalName}'. Expected 'nmaprun'.");

        try
        {
            var result = MapNmapRun(root);
            return NmapParseResult.Success(result);
        }
        catch (Exception ex)
        {
            return NmapParseResult.Failure($"Unable to map Nmap XML: {ex.Message}");
        }
    }

    private static XDocument LoadSecure(string xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = 50_000_000
        };

        using var stringReader = new StringReader(xml);
        using var reader = XmlReader.Create(stringReader, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static NmapScanResult MapNmapRun(XElement root)
    {
        var startRaw = Attr(root, "start");
        DateTimeOffset? startTime = null;
        if (long.TryParse(startRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix) && unix > 0)
        {
            try
            {
                startTime = DateTimeOffset.FromUnixTimeSeconds(unix);
            }
            catch
            {
                startTime = null;
            }
        }

        double? elapsed = null;
        string? summary = null;
        string? exitStatus = null;

        var runstats = root.Element("runstats");
        var finished = runstats?.Element("finished");
        if (finished is not null)
        {
            var elapsedAttr = Attr(finished, "elapsed");
            if (double.TryParse(elapsedAttr, NumberStyles.Float, CultureInfo.InvariantCulture, out var e))
                elapsed = e;
            summary = Attr(finished, "summary") ?? Attr(finished, "exit");
            exitStatus = Attr(finished, "exit");
            if (!string.IsNullOrWhiteSpace(Attr(finished, "summary")))
                summary = Attr(finished, "summary");
        }

        var hosts = root.Elements("host")
            .Select(MapHost)
            .ToList();

        return new NmapScanResult
        {
            Scanner = Attr(root, "scanner"),
            Version = Attr(root, "version"),
            Arguments = Attr(root, "args"),
            StartTimeRaw = startRaw ?? Attr(root, "startstr"),
            StartTime = startTime,
            ElapsedSeconds = elapsed,
            Summary = summary,
            ExitStatus = exitStatus,
            Hosts = hosts
        };
    }

    private static NmapHostResult MapHost(XElement host)
    {
        var status = host.Element("status");
        var addresses = host.Elements("address")
            .Select(a => new NmapAddressResult
            {
                Address = Attr(a, "addr"),
                AddressType = Attr(a, "addrtype"),
                Vendor = Attr(a, "vendor")
            })
            .ToList();

        var hostnames = host.Element("hostnames")?
            .Elements("hostname")
            .Select(h => new NmapHostnameResult
            {
                Name = Attr(h, "name"),
                Type = Attr(h, "type")
            })
            .ToList()
            ?? new List<NmapHostnameResult>();

        var ports = host.Element("ports")?
            .Elements("port")
            .Select(MapPort)
            .ToList()
            ?? new List<NmapPortResult>();

        return new NmapHostResult
        {
            State = Attr(status, "state"),
            StateReason = Attr(status, "reason"),
            Addresses = addresses,
            Hostnames = hostnames,
            Ports = ports
        };
    }

    private static NmapPortResult MapPort(XElement port)
    {
        var state = port.Element("state");
        var service = port.Element("service");

        int? portId = null;
        var portIdAttr = Attr(port, "portid");
        if (int.TryParse(portIdAttr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) && pid >= 0 && pid <= 65535)
            portId = pid;

        NmapServiceResult? svc = null;
        if (service is not null)
        {
            int? conf = null;
            if (int.TryParse(Attr(service, "conf"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var c))
                conf = c;

            svc = new NmapServiceResult
            {
                Name = Attr(service, "name"),
                Product = Attr(service, "product"),
                Version = Attr(service, "version"),
                ExtraInfo = Attr(service, "extrainfo"),
                Tunnel = Attr(service, "tunnel"),
                Method = Attr(service, "method"),
                Confidence = conf
            };
        }

        return new NmapPortResult
        {
            Protocol = Attr(port, "protocol"),
            PortId = portId,
            State = Attr(state, "state"),
            Reason = Attr(state, "reason"),
            Service = svc
        };
    }

    private static string? Attr(XElement? element, string name)
    {
        if (element is null) return null;
        var value = (string?)element.Attribute(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
