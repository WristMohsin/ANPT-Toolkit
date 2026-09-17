using ANPT.Application.Interfaces;
using ANPT.Application.Services;
using ANPT.Infrastructure.Configuration;
using ANPT.Infrastructure.Data;
using ANPT.Infrastructure.Repositories;
using ANPT.Infrastructure.Security;
using ANPT.Infrastructure.Scanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ANPT.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        AppPaths.EnsureDirectoriesExist();

        services.AddDbContext<AnptDbContext>(options =>
            options.UseSqlite($"Data Source={AppPaths.DatabasePath}"));

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITargetRepository, TargetRepository>();
        services.AddScoped<IScanRepository, ScanRepository>();
        services.AddScoped<IScanProfileRepository, ScanProfileRepository>();
        services.AddScoped<DatabaseInitializer>();
        services.AddSingleton<INmapProcessRunner, NmapProcessRunner>();
        services.AddSingleton<IScanProcessTracker, ScanProcessTracker>();
        services.AddSingleton<IScanOutputPathService, ScanOutputPathService>();
        services.AddSingleton<INmapXmlResultReader, NmapXmlResultReader>();
        services.AddScoped<INmapResultPersistenceService, NmapResultPersistenceService>();
        services.AddScoped<IFindingRepository, FindingRepository>();
        services.AddScoped<IScanHostLoader, ScanHostLoader>();

        return services;
    }
}
