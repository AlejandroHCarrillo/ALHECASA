using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Auth;
using Alhecasa.Application.Common;
using Alhecasa.Application.Dashboard;
using Alhecasa.Application.Inquiries;
using Alhecasa.Application.Properties;
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

    public Agency AddIndividual(string name = "Particular")
    {
        var agency = Agency.CreateIndividual(name, $"{Guid.NewGuid():N}@individual.test");
        _agencies.Add(agency);
        return agency;
    }

    public Task AddAsync(Agency agency, CancellationToken cancellationToken = default)
    {
        _agencies.Add(agency);
        return Task.CompletedTask;
    }

    public Task<Agency?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_agencies.FirstOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<Agency>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Agency>>(_agencies.ToList());
}

internal sealed class FakePropertyRepository : IPropertyRepository
{
    public List<Property> Items { get; } = [];

    /// <summary>Últimas consultas recibidas, para comprobar lo que el servicio normaliza y envía. Los criterios los aplica el repositorio real (pruebas de integración).</summary>
    public PropertyListQuery? LastListQuery { get; private set; }

    public PublicPropertyQuery? LastPublicQuery { get; private set; }

    public Guid? LastPublicAgencyId { get; private set; }

    public Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Id == id && !p.IsDeleted));

    public Task<PagedResult<Property>> GetPagedAsync(PropertyListQuery query, CancellationToken cancellationToken = default)
    {
        LastListQuery = query;
        var items = Items.Where(p => !p.IsDeleted).ToList();
        return Task.FromResult(new PagedResult<Property>(items, query.Page, query.PageSize, items.Count));
    }

    public Task<Property?> GetPublishedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Id == id && !p.IsDeleted && p.Status == PropertyStatus.Published));

    public Task<PagedResult<Property>> GetPublishedPagedAsync(PublicPropertyQuery query, Guid? agencyId = null, CancellationToken cancellationToken = default)
    {
        LastPublicQuery = query;
        LastPublicAgencyId = agencyId;
        var items = Items
            .Where(p => !p.IsDeleted && p.Status == PropertyStatus.Published && (agencyId is null || p.AgencyId == agencyId) && (query.AgencyId is null || p.AgencyId == query.AgencyId) && (query.Ids is null || query.Ids.Contains(p.Id)))
            .ToList();
        return Task.FromResult(new PagedResult<Property>(items, query.Page, query.PageSize, items.Count));
    }

    public Task<PropertyImage?> GetImageForServingAsync(Guid imageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(p => !p.IsDeleted).SelectMany(p => p.Images).FirstOrDefault(i => i.Id == imageId));

    public Task<int> CountByAgencyAsync(Guid agencyId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Count(p => p.AgencyId == agencyId && !p.IsDeleted));

    public Task<IReadOnlyList<Property>> ListFeaturedAsync(Guid agencyId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Property>>(Items.Where(p => p.AgencyId == agencyId && !p.IsDeleted && p.IsFeaturedAt(now)).ToList());

    public Task<IReadOnlyDictionary<Guid, PropertyUsage>> GetUsageByAgencyAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, PropertyUsage>>(Items
            .Where(p => !p.IsDeleted)
            .GroupBy(p => p.AgencyId)
            .ToDictionary(g => g.Key, g => new PropertyUsage(g.Count(), g.Count(p => p.IsFeaturedAt(now)))));

    public Task<PropertyStats> GetStatsAsync(Guid agencyId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var mine = Items.Where(p => p.AgencyId == agencyId && !p.IsDeleted).ToList();
        int Of(PropertyStatus status) => mine.Count(p => p.Status == status);
        return Task.FromResult(new PropertyStats(
            mine.Count,
            Of(PropertyStatus.Published),
            Of(PropertyStatus.Draft),
            Of(PropertyStatus.Paused),
            Of(PropertyStatus.Closed),
            mine.Count(p => p.IsFeaturedAt(now))));
    }

    public Task AddAsync(Property entity, CancellationToken cancellationToken = default)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }
}

internal sealed class FakeInquiryRepository : IInquiryRepository
{
    public List<Inquiry> Items { get; } = [];

    public Task<Inquiry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == id && !i.IsDeleted));

    public Task<PagedResult<Inquiry>> GetPagedAsync(InquiryListQuery query, CancellationToken cancellationToken = default)
    {
        var items = Items
            .Where(i => !i.IsDeleted && (query.Status is null || i.Status == query.Status))
            .ToList();
        return Task.FromResult(new PagedResult<Inquiry>(items, query.Page, query.PageSize, items.Count));
    }

    public Task<InquiryStats> GetStatsAsync(Guid agencyId, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var mine = Items.Where(i => i.AgencyId == agencyId && !i.IsDeleted).ToList();
        int Of(InquiryStatus status) => mine.Count(i => i.Status == status);
        return Task.FromResult(new InquiryStats(
            Of(InquiryStatus.New),
            Of(InquiryStatus.InProgress),
            Of(InquiryStatus.Closed),
            mine.Count(i => i.CreatedAt >= since)));
    }

    public Task<IReadOnlyList<Inquiry>> ListLatestNewAsync(Guid agencyId, int count, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Inquiry>>(Items
            .Where(i => i.AgencyId == agencyId && !i.IsDeleted && i.Status == InquiryStatus.New)
            .OrderByDescending(i => i.CreatedAt)
            .Take(count)
            .ToList());

    public Task AddAsync(Inquiry inquiry, CancellationToken cancellationToken = default)
    {
        Items.Add(inquiry);
        return Task.CompletedTask;
    }
}

/// <summary>Almacenamiento en memoria para las pruebas de unidad.</summary>
internal sealed class FakeImageStorage : IImageStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Files[key] = buffer.ToArray();
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(Files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Files.Remove(key);
        return Task.CompletedTask;
    }
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

/// <summary>Reloj controlable para las pruebas de contrataciones y destacados.</summary>
internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}

internal sealed class FakePlanRepository : IPlanRepository
{
    public List<Plan> Items { get; } = [];

    public Plan Add(PlanData data)
    {
        var plan = new Plan(data);
        Items.Add(plan);
        return plan;
    }

    public Task<Plan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Id == id && !p.IsDeleted));

    public Task<IReadOnlyList<Plan>> ListAsync(PlanKind? kind = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Plan>>(Items.Where(p => !p.IsDeleted && (kind is null || p.Kind == kind)).ToList());

    public Task<Plan?> GetActiveFreeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(p => !p.IsDeleted && p.IsActive && p.IsFree));

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(p => !p.IsDeleted && p.Name == name && p.Id != excludeId));

    public Task AddAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        Items.Add(plan);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSubscriptionRepository : ISubscriptionRepository
{
    public List<Subscription> Items { get; } = [];

    public Task<Subscription?> GetActiveAsync(Guid agencyId, PlanKind kind, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(s => s.AgencyId == agencyId && s.Kind == kind && s.IsActiveAt(now)).OrderByDescending(s => s.StartsAt).FirstOrDefault());

    public Task<IReadOnlyList<Subscription>> ListByAgencyAsync(Guid agencyId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Subscription>>(Items.Where(s => s.AgencyId == agencyId).OrderByDescending(s => s.StartsAt).ToList());

    public Task<IReadOnlyList<Subscription>> ListActiveAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Subscription>>(Items.Where(s => s.IsActiveAt(now)).ToList());

    public Task<Subscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(s => s.Id == id));

    public Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        Items.Add(subscription);
        return Task.CompletedTask;
    }
}

/// <summary>Pasarela de pagos de prueba: aprueba o rechaza según se configure y guarda los cobros que recibió.</summary>
internal sealed class FakePaymentGateway : IPaymentGateway
{
    public List<PaymentRequest> Charges { get; } = [];

    /// <summary>Motivo con el que se rechaza el siguiente cobro. Nulo = se aprueba.</summary>
    public string? DeclineWith { get; set; }

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        Charges.Add(request);
        return Task.FromResult(DeclineWith is null ? PaymentResult.Approve($"TX-{Charges.Count}") : PaymentResult.Decline(DeclineWith));
    }
}
