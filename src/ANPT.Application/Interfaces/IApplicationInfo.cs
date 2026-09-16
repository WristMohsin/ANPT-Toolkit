namespace ANPT.Application.Interfaces;

public interface IApplicationInfo
{
    string ApplicationName { get; }
    string ShortName { get; }
    string Version { get; }
    string Description { get; }
}
