using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Auth;
using Alhecasa.Application.Common;
using Alhecasa.Application.Users;
using Alhecasa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AlhecasaDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Users
            .Include(u => u.Agency)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<PagedResult<User>> GetPagedAsync(UserListQuery query, CancellationToken cancellationToken = default)
    {
        IQueryable<User> source = db.Users.AsNoTracking().Include(u => u.Agency);

        if (query.Search is not null)
        {
            var pattern = $"%{EscapeLike(query.Search)}%";
            source = source.Where(u =>
                EF.Functions.Like(u.FirstName, pattern, "\\")
                || EF.Functions.Like(u.LastName, pattern, "\\")
                || EF.Functions.Like(u.Email, pattern, "\\"));
        }

        if (query.Role is not null)
        {
            source = source.Where(u => u.Role == query.Role);
        }

        if (query.IsActive is not null)
        {
            source = source.Where(u => u.IsActive == query.IsActive);
        }

        var total = await source.CountAsync(cancellationToken);

        var items = await source
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ThenBy(u => u.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<User>(items, query.Page, query.PageSize, total);
    }

    public Task<User?> GetByEmailForAuthenticationAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Agency)
            .Where(u => !u.IsDeleted && u.Email == normalizedEmail)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<UserSessionState?> GetSessionStateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Id == userId && !u.IsDeleted)
            .Select(u => new UserSessionState(
                u.Role,
                u.AgencyId,
                u.IsActive,
                u.Agency == null || (u.Agency.IsActive && !u.Agency.IsDeleted)))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken cancellationToken = default) =>
        db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => !u.IsDeleted && u.Email == normalizedEmail && u.Id != excludeUserId, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await db.Users.AddAsync(user, cancellationToken);

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
