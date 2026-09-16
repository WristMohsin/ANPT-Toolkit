using ANPT.Application.Interfaces;

namespace ANPT.Application.Services;

public class ApplicationInfo : IApplicationInfo
{
    public string ApplicationName => "Automated Network Penetration Testing Toolkit";
    public string ShortName => "ANPT Toolkit";
    public string Version => "1.0.0-phase4";
    public string Description => "Professional Windows desktop automated network security assessment toolkit for authorized penetration testing.";
}
