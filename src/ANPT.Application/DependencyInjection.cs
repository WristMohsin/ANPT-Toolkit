using ANPT.Application.Analysis;
using ANPT.Application.Interfaces;
using ANPT.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ANPT.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IApplicationInfo, ApplicationInfo>();
        services.AddSingleton<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<ITargetService, TargetService>();
        services.AddScoped<IScanService, ScanService>();
        services.AddScoped<IScanExecutionService, ScanExecutionService>();
        services.AddSingleton<INmapXmlParser, NmapXmlParser>();

        // Phase 5E — security analysis foundation
        services.AddSingleton<IAnalysisRule, OpenServiceExposureRule>();
        services.AddSingleton<IAnalysisRule, ServiceWithoutIdentificationRule>();
        services.AddSingleton<IAnalysisRule, SensitiveServiceExposureRule>();
        services.AddScoped<ISecurityAnalysisService, SecurityAnalysisService>();
        services.AddScoped<IFindingService, FindingService>();

        return services;
    }
}
