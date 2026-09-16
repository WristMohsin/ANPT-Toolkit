using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ANPT.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AnptDbContext _db;

    public UserRepository(AnptDbContext db)
    {
        _db = db;
    }

    public async Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim();
        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == normalized, cancellationToken);
    }

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim();
        return await _db.Users.AnyAsync(u => u.Username == normalized, cancellationToken);
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        user.Username = user.Username.Trim();
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        var tracked = _db.ChangeTracker.Entries<User>()
            .FirstOrDefault(e => e.Entity.Id == user.Id);
        if (tracked is not null)
            tracked.State = EntityState.Detached;

        _db.Users.Update(user);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
