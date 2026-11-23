using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Auth;
using Alhecasa.Application.Common;
using Alhecasa.Application.Users;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;

namespace Alhecasa.UnitTests.Support;

internal sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => UserId is not null;

    public Guid? UserId { get; init; }

    public UserRole? Role { get; init; }

    public Guid? AgencyId { get; init; }

    public bool IsPlatformAdmin => Role == UserRole.PlatformAdmin;

    public static FakeCurrentUser PlatformAdmin(Guid? id = null) =>
        new() { UserId = id ?? Guid.CreateVersion7(), Role = UserRole.PlatformAdmin };

    public static FakeCurrentUser AgencyAdmin(Guid agencyId, Guid? id = null) =>
        new() { UserId = id ?? Guid.CreateVersion7(), Role = UserRole.AgencyAdmin, AgencyId = agencyId };

    public static FakeCurrentUser Client(Guid? id = null) =>
        new() { UserId = id ?? Guid.CreateVersion7(), Role = UserRole.Client };
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(0);
    }
}

/// <summary>Hasher sin costo computacional: el comportamiento real se prueba en Pbkdf2PasswordHasherTests.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hash:{password}";

    public bool Verify(string password, string passwordHash) => passwordHash == $"hash:{password}";

    public int DummyVerifications { get; private set; }

    public void VerifyDummy(string password) => DummyVerifications++;
}

internal sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
{
    public AccessToken Generate(User user) => new($"token-for-{user.Id}", DateTimeOffset.UtcNow.AddHours(1));
}

internal sealed class FakeAgencyRepository : IAgencyRepository
{
    private readonly List<Agency> _agencies = [];

    public Agency Add(string name = "Demo")
    {
        var agency = new Agency(name, $"{Guid.NewGuid():N}@agency.test", null);
        _agencies.Add(agency);
        return agency;
    }

    public Task<Agency?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_agencies.FirstOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<Agency>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Agency>>(_agencies.ToList());
}

internal sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Dictionary<Guid, UserSessionState> SessionStates { get; } = [];

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id && !u.IsDeleted));

    public Task<PagedResult<User>> GetPagedAsync(UserListQuery query, CancellationToken cancellationToken = default)
    {
        var items = Users.Where(u => !u.IsDeleted).ToList();
        return Task.FromResult(new PagedResult<User>(items, query.Page, query.PageSize, items.Count));
    }

    public Task<User?> GetByEmailForAuthenticationAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => !u.IsDeleted && u.Email == normalizedEmail));

    public Task<UserSessionState?> GetSessionStateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SessionStates.TryGetValue(userId, out var state) ? state : null);

    public Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Any(u => !u.IsDeleted && u.Email == normalizedEmail && u.Id != excludeUserId));

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }
}
