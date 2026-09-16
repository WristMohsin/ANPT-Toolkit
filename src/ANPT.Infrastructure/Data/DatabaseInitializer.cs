using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ANPT.Infrastructure.Data;

public class DatabaseInitializer
{
    private readonly AnptDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DatabaseInitializer> _logger;

    private const string BootstrapUsername = "admin";
    private const string BootstrapPassword = "Admin@ChangeMe1";

    public DatabaseInitializer(
        AnptDbContext db,
        IPasswordHasher passwordHasher,
        ILogger<DatabaseInitializer> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Ensuring database is created...");
            await _db.Database.EnsureCreatedAsync();

            if (!await _db.Users.AnyAsync())
            {
                _logger.LogInformation("Seeding bootstrap administrator account...");
                var hash = _passwordHasher.HashPassword(BootstrapPassword);

                _db.Users.Add(new User
                {
                    Username = BootstrapUsername,
                    DisplayName = "Administrator",
                    PasswordHash = hash,
                    Role = UserRole.Admin,
                    IsActive = true
                });
                await _db.SaveChangesAsync();
                _logger.LogInformation("Bootstrap administrator account created. Change the default password after first login.");
            }

            _logger.LogInformation("Database initialization completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database initialization failed.");
            throw;
        }
    }
}
