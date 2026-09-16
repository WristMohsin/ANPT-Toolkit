using ANPT.Application.Interfaces;
using ANPT.Infrastructure.Configuration;
using ANPT.Infrastructure.Data;
using ANPT.Infrastructure.Repositories;
using ANPT.Infrastructure.Security;
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
        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}
