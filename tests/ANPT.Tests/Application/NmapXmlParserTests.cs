using ANPT.Application.Models.Nmap;
using ANPT.Application.Services;

namespace ANPT.Tests.Application;

public class NmapXmlParserTests
{
    private readonly NmapXmlParser _parser = new();

    private const string MinimalXml = """
        <?xml version="1.0"?>
        <nmaprun scanner="nmap" args="nmap -sn 10.0.0.1" start="1700000000" startstr="Tue Nov 14 22:13:20 2023" version="7.94">
          <runstats>
            <finished time="1700000010" elapsed="10.5" summary="Nmap done at Tue Nov 14 22:13:30 2023; 1 IP address (0 hosts up) scanned in 10.50 seconds" exit="success"/>
          </runstats>
        </nmaprun>
        """;

    private const string FullFixture = """
        <?xml version="1.0"?>
        <nmaprun scanner="nmap" args="nmap -sT -sV 10.0.0.1" start="1700000000" version="7.94">
          <host>
            <status state="up" reason="syn-ack"/>
            <address addr="10.0.0.1" addrtype="ipv4"/>
            <address addr="00:11:22:33:44:55" addrtype="mac" vendor="VendorCo"/>
            <hostnames>
              <hostname name="lab.local" type="PTR"/>
              <hostname name="lab" type="user"/>
            </hostnames>
            <ports>
              <port protocol="tcp" portid="22">
                <state state="open" reason="syn-ack"/>
                <service name="ssh" product="OpenSSH" version="8.9p1" extrainfo="Ubuntu" method="probed" conf="10"/>
              </port>
              <port protocol="tcp" portid="80">
                <state state="open" reason="syn-ack"/>
                <service name="http" product="nginx" version="1.18.0" tunnel="ssl" method="probed" conf="10"/>
              </port>
              <port protocol="tcp" portid="443">
                <state state="filtered" reason="no-response"/>
              </port>
              <port protocol="tcp" portid="9999">
                <state state="closed" reason="reset"/>
                <service name="abyss"/>
              </port>
            </ports>
          </host>
          <host>
            <status state="up" reason="echo-reply"/>
            <address addr="2001:db8::1" addrtype="ipv6"/>
            <ports>
              <port protocol="tcp" portid="22">
                <state state="open" reason="syn-ack"/>
                <service name="ssh"/>
              </port>
            </ports>
          </host>
          <runstats>
            <finished time="1700000120" elapsed="120.25" summary="Nmap done; 2 IP addresses (2 hosts up) scanned in 120.25 seconds" exit="success"/>
          </runstats>
        </nmaprun>
        """;

    [Fact]
    public void Parse_Minimal_SucceedsWithMetadata()
    {
        var result = _parser.Parse(MinimalXml);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Scan);
        Assert.Equal("nmap", result.Scan.Scanner);
        Assert.Equal("7.94", result.Scan.Version);
        Assert.Contains("nmap -sn", result.Scan.Arguments!);
        Assert.Equal("1700000000", result.Scan.StartTimeRaw);
        Assert.NotNull(result.Scan.StartTime);
        Assert.Equal(10.5, result.Scan.ElapsedSeconds);
        Assert.Contains("Nmap done", result.Scan.Summary!);
        Assert.Equal("success", result.Scan.ExitStatus);
        Assert.Empty(result.Scan.Hosts);
    }

    [Fact]
    public void Parse_FullFixture_MapsHostsPortsServices()
    {
        var result = _parser.Parse(FullFixture);
        Assert.True(result.Succeeded);
        var scan = result.Scan!;
        Assert.Equal(2, scan.Hosts.Count);

        var h1 = scan.Hosts[0];
        Assert.Equal("up", h1.State);
        Assert.Equal("syn-ack", h1.StateReason);
        Assert.Equal(2, h1.Addresses.Count);
        Assert.Equal("10.0.0.1", h1.Addresses[0].Address);
        Assert.Equal("ipv4", h1.Addresses[0].AddressType);
        Assert.Equal("00:11:22:33:44:55", h1.Addresses[1].Address);
        Assert.Equal("mac", h1.Addresses[1].AddressType);
        Assert.Equal("VendorCo", h1.Addresses[1].Vendor);
        Assert.Equal(2, h1.Hostnames.Count);
        Assert.Equal("lab.local", h1.Hostnames[0].Name);
        Assert.Equal(4, h1.Ports.Count);

        var ssh = h1.Ports[0];
        Assert.Equal("tcp", ssh.Protocol);
        Assert.Equal(22, ssh.PortId);
        Assert.Equal("open", ssh.State);
        Assert.Equal("syn-ack", ssh.Reason);
        Assert.NotNull(ssh.Service);
        Assert.Equal("ssh", ssh.Service.Name);
        Assert.Equal("OpenSSH", ssh.Service.Product);
        Assert.Equal("8.9p1", ssh.Service.Version);
        Assert.Equal("Ubuntu", ssh.Service.ExtraInfo);
        Assert.Equal("probed", ssh.Service.Method);
        Assert.Equal(10, ssh.Service.Confidence);

        var http = h1.Ports[1];
        Assert.Equal(80, http.PortId);
        Assert.Equal("nginx", http.Service!.Product);
        Assert.Equal("ssl", http.Service.Tunnel);

        var filtered = h1.Ports[2];
        Assert.Equal(443, filtered.PortId);
        Assert.Equal("filtered", filtered.State);
        Assert.Null(filtered.Service);

        var closed = h1.Ports[3];
        Assert.Equal("closed", closed.State);
        Assert.Equal("abyss", closed.Service!.Name);
        Assert.Null(closed.Service.Product);

        var h2 = scan.Hosts[1];
        Assert.Equal("2001:db8::1", h2.Addresses[0].Address);
        Assert.Equal("ipv6", h2.Addresses[0].AddressType);
        Assert.Empty(h2.Hostnames);
        Assert.Single(h2.Ports);
        Assert.Equal(22, h2.Ports[0].PortId);

        Assert.Equal(120.25, scan.ElapsedSeconds);
        Assert.Contains("2 IP addresses", scan.Summary!);
    }

    [Fact]
    public void Parse_HostWithNoPorts_Succeeds()
    {
        const string xml = """
            <nmaprun scanner="nmap" version="7.94">
              <host>
                <status state="down" reason="no-response"/>
                <address addr="10.0.0.99" addrtype="ipv4"/>
              </host>
            </nmaprun>
            """;
        var result = _parser.Parse(xml);
        Assert.True(result.Succeeded);
        Assert.Single(result.Scan!.Hosts);
        Assert.Equal("down", result.Scan.Hosts[0].State);
        Assert.Empty(result.Scan.Hosts[0].Ports);
        Assert.Empty(result.Scan.Hosts[0].Hostnames);
    }

    [Fact]
    public void Parse_PortWithoutService_Succeeds()
    {
        const string xml = """
            <nmaprun scanner="nmap">
              <host>
                <status state="up"/>
                <address addr="10.0.0.1" addrtype="ipv4"/>
                <ports>
                  <port protocol="tcp" portid="8080">
                    <state state="open" reason="syn-ack"/>
                  </port>
                </ports>
              </host>
            </nmaprun>
            """;
        var result = _parser.Parse(xml);
        Assert.True(result.Succeeded);
        var port = result.Scan!.Hosts[0].Ports[0];
        Assert.Equal(8080, port.PortId);
        Assert.Null(port.Service);
    }

    [Fact]
    public void Parse_InvalidPortNumber_LeavesPortIdNull()
    {
        const string xml = """
            <nmaprun scanner="nmap">
              <host>
                <status state="up"/>
                <ports>
                  <port protocol="tcp" portid="not-a-number">
                    <state state="open"/>
                  </port>
                  <port protocol="tcp" portid="99999">
                    <state state="open"/>
                  </port>
                </ports>
              </host>
            </nmaprun>
            """;
        var result = _parser.Parse(xml);
        Assert.True(result.Succeeded);
        Assert.Null(result.Scan!.Hosts[0].Ports[0].PortId);
        Assert.Null(result.Scan.Hosts[0].Ports[1].PortId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Empty_Fails(string? xml)
    {
        var result = _parser.Parse(xml!);
        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Parse_MalformedXml_Fails()
    {
        var result = _parser.Parse("<nmaprun><host></nmaprun>");
        Assert.False(result.Succeeded);
        Assert.Contains("Malformed", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_WrongRoot_Fails()
    {
        var result = _parser.Parse("<root><child/></root>");
        Assert.False(result.Succeeded);
        Assert.Contains("nmaprun", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ExternalEntity_DoesNotResolve()
    {
        const string xxe = """
            <?xml version="1.0"?>
            <!DOCTYPE nmaprun [
              <!ENTITY xxe SYSTEM "file:///etc/passwd">
            ]>
            <nmaprun scanner="nmap">&xxe;</nmaprun>
            """;
        var result = _parser.Parse(xxe);
        Assert.False(result.Succeeded);
        Assert.True(
            result.Scan is null ||
            (result.Scan.Arguments is null && result.Scan.Summary is null));
    }

    [Fact]
    public void Parse_ExternalDtd_IsProhibited()
    {
        const string dtd = """
            <?xml version="1.0"?>
            <!DOCTYPE nmaprun SYSTEM "http://evil.example/nmap.dtd">
            <nmaprun scanner="nmap" version="7.94"/>
            """;
        var result = _parser.Parse(dtd);
        Assert.NotNull(result);
        if (result.Succeeded)
            Assert.Equal("nmap", result.Scan!.Scanner);
    }

    [Fact]
    public void Parse_MissingOptionalAttributes_UsesNulls()
    {
        const string xml = """
            <nmaprun>
              <host>
                <status/>
                <ports>
                  <port>
                    <state/>
                    <service/>
                  </port>
                </ports>
              </host>
            </nmaprun>
            """;
        var result = _parser.Parse(xml);
        Assert.True(result.Succeeded);
        var host = result.Scan!.Hosts[0];
        Assert.Null(host.State);
        Assert.Null(host.StateReason);
        Assert.Null(result.Scan.Scanner);
        var port = host.Ports[0];
        Assert.Null(port.Protocol);
        Assert.Null(port.PortId);
        Assert.NotNull(port.Service);
        Assert.Null(port.Service.Name);
    }
}
