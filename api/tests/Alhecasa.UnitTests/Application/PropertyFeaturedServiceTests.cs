using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Billing;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.UnitTests.Domain;
using Alhecasa.UnitTests.Support;

namespace Alhecasa.UnitTests.Application;

/// <summary>Planes de publicaciones y destacados aplicados al inventario: tope al crear, y destacar anuncios con un paquete.</summary>
public sealed class PropertyFeaturedServiceTests
{
    private readonly FakePropertyRepository _properties = new();
    private readonly FakeAgencyRepository _agencies = new();
    private readonly FakePlanRepository _plans = new();
    private readonly FakeSubscriptionRepository _subscriptions = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly Agency _company;

    public PropertyFeaturedServiceTests() => _company = _agencies.Add("Inmobiliaria");

    private PropertyService CreateService(ICurrentUser user) => new(
        _properties,
        _agencies,
        new FakeImageStorage(),
        _unitOfWork,
        user,
        new CreatePropertyRequestValidator(),
        new UpdatePropertyRequestValidator(),
        _clock,
        new EntitlementService(_subscriptions, _plans, _properties, _clock, new AccountLimits(3)));

    private PropertyService CompanyService() => CreateService(FakeCurrentUser.AgencyAdmin(_company.Id));

    private static CreatePropertyRequest NewRequest() => new()
    {
        Title = "Casa en Polanco",
        Type = PropertyType.House,
        Operation = OperationType.Sale,
        Price = 4_500_000m,
        City = "Ciudad de México",
        State = "CDMX"
    };

    private Property AddProperty(Guid? agencyId = null, PropertyStatus status = PropertyStatus.Published)
    {
        var property = new Property(agencyId ?? _company.Id, PropertyTests.ValidData() with { Status = status });
        _properties.Items.Add(property);
        return property;
    }

    private void Subscribe(Guid agencyId, PlanData data)
    {
        var plan = _plans.Add(data);
        _subscriptions.Items.Add(new Subscription(agencyId, plan, _clock.Now, plan.Price, null));
    }

    private static PlanData FeaturedPackage(int? limit, string name = "3 destacados") =>
        PlanTests.Paid(PlanKind.Featured, limit, BillingPeriod.Week, 199m) with { Name = name, Audience = PlanAudience.Any };

    // --- Tope de publicaciones ------------------------------------------------------------

    [Fact]
    public async Task ACompanyWithAListingsPlan_CanCreateUpToItsLimit_ButNotOneMore()
    {
        Subscribe(_company.Id, PlanTests.Paid(limit: 2));
        AddProperty();
        AddProperty();

        var ex = await Assert.ThrowsAsync<PlanLimitException>(() => CompanyService().CreateAsync(NewRequest()));

        Assert.Contains("Inmobiliaria 100", ex.Message, StringComparison.Ordinal);
        Assert.Contains("hasta 2 propiedades", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACompanyWithAListingsPlan_CanCreateWhileItHasRoom()
    {
        Subscribe(_company.Id, PlanTests.Paid(limit: 2));
        AddProperty();

        var created = await CompanyService().CreateAsync(NewRequest());

        Assert.Equal(_company.Id, created.AgencyId);
    }

    [Fact]
    public async Task AnUnlimitedPlan_NeverBlocks()
    {
        Subscribe(_company.Id, PlanTests.Paid(limit: null));
        for (var i = 0; i < 10; i++)
        {
            AddProperty();
        }

        await CompanyService().CreateAsync(NewRequest());
    }

    [Fact]
    public async Task WhenThePlanExpires_TheCompanyGoesBackToHavingNoLimit_AndAnIndividualToTheFreePlan()
    {
        var individual = _agencies.AddIndividual();
        _plans.Add(PlanTests.Free(limit: 1));
        Subscribe(individual.Id, PlanTests.Paid(limit: 5) with { Audience = PlanAudience.Individual, Name = "Particular 5" });
        AddProperty(individual.Id);
        AddProperty(individual.Id);
        var service = CreateService(FakeCurrentUser.AgencyAdmin(individual.Id));

        await service.CreateAsync(NewRequest());
        _clock.Advance(TimeSpan.FromDays(40));

        await Assert.ThrowsAsync<PlanLimitException>(() => service.CreateAsync(NewRequest()));
    }

    [Fact]
    public async Task TheFreePlanLimit_ComesFromTheCatalog()
    {
        var individual = _agencies.AddIndividual();
        _plans.Add(PlanTests.Free(limit: 1));
        AddProperty(individual.Id);

        var ex = await Assert.ThrowsAsync<PlanLimitException>(() => CreateService(FakeCurrentUser.AgencyAdmin(individual.Id)).CreateAsync(NewRequest()));

        Assert.Contains("hasta 1 propiedad.", ex.Message, StringComparison.Ordinal);
    }

    // --- Destacar -------------------------------------------------------------------------

    [Fact]
    public async Task Feature_NeedsAnActivePackage()
    {
        var property = AddProperty();

        var ex = await Assert.ThrowsAsync<PlanLimitException>(() => CompanyService().SetFeaturedAsync(property.Id, new SetFeaturedRequest(true)));

        Assert.Contains("paquete de destacados", ex.Message, StringComparison.Ordinal);
        Assert.False(property.IsFeaturedAt(_clock.Now));
    }

    [Fact]
    public async Task Feature_LastsUntilThePackageEnds()
    {
        Subscribe(_company.Id, FeaturedPackage(3));
        var property = AddProperty();

        var dto = await CompanyService().SetFeaturedAsync(property.Id, new SetFeaturedRequest(true));

        Assert.True(dto.IsFeatured);
        Assert.Equal(_clock.Now.AddDays(7), dto.FeaturedUntil);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Feature_StopsAtTheLimitOfThePackage()
    {
        Subscribe(_company.Id, FeaturedPackage(2));
        var service = CompanyService();
        await service.SetFeaturedAsync(AddProperty().Id, new SetFeaturedRequest(true));
        await service.SetFeaturedAsync(AddProperty().Id, new SetFeaturedRequest(true));
        var third = AddProperty();

        var ex = await Assert.ThrowsAsync<PlanLimitException>(() => service.SetFeaturedAsync(third.Id, new SetFeaturedRequest(true)));

        Assert.Contains("2 anuncios destacados", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unfeaturing_FreesAPlaceInThePackage()
    {
        Subscribe(_company.Id, FeaturedPackage(1));
        var service = CompanyService();
        var first = AddProperty();
        var second = AddProperty();
        await service.SetFeaturedAsync(first.Id, new SetFeaturedRequest(true));

        var removed = await service.SetFeaturedAsync(first.Id, new SetFeaturedRequest(false));
        var dto = await service.SetFeaturedAsync(second.Id, new SetFeaturedRequest(true));

        Assert.False(removed.IsFeatured);
        Assert.Null(removed.FeaturedUntil);
        Assert.True(dto.IsFeatured);
    }

    [Fact]
    public async Task AnUnlimitedPackage_HasNoCap()
    {
        Subscribe(_company.Id, FeaturedPackage(null, "Destacados ilimitados"));
        var service = CompanyService();

        for (var i = 0; i < 12; i++)
        {
            Assert.True((await service.SetFeaturedAsync(AddProperty().Id, new SetFeaturedRequest(true))).IsFeatured);
        }
    }

    [Fact]
    public async Task OnlyAPublishedProperty_CanBeFeatured()
    {
        Subscribe(_company.Id, FeaturedPackage(3));
        var draft = AddProperty(status: PropertyStatus.Draft);

        await Assert.ThrowsAsync<DomainException>(() => CompanyService().SetFeaturedAsync(draft.Id, new SetFeaturedRequest(true)));
    }

    [Fact]
    public async Task FeaturingAnAlreadyFeaturedProperty_ChangesNothingAndDoesNotCountTwice()
    {
        Subscribe(_company.Id, FeaturedPackage(1));
        var service = CompanyService();
        var property = AddProperty();
        await service.SetFeaturedAsync(property.Id, new SetFeaturedRequest(true));
        var saves = _unitOfWork.SaveCount;

        var again = await service.SetFeaturedAsync(property.Id, new SetFeaturedRequest(true));

        Assert.True(again.IsFeatured);
        Assert.Equal(saves, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnExpiredFeature_StopsCountingAgainstThePackage()
    {
        Subscribe(_company.Id, FeaturedPackage(1));
        var service = CompanyService();
        var first = AddProperty();
        await service.SetFeaturedAsync(first.Id, new SetFeaturedRequest(true));
        _clock.Advance(TimeSpan.FromDays(8));

        Assert.False((await service.GetByIdAsync(first.Id)).IsFeatured);
        await Assert.ThrowsAsync<PlanLimitException>(() => service.SetFeaturedAsync(AddProperty().Id, new SetFeaturedRequest(true)));
    }

    [Fact]
    public async Task APropertyThatIsNoLongerPublished_LosesItsFeature()
    {
        Subscribe(_company.Id, FeaturedPackage(3));
        var service = CompanyService();
        var property = AddProperty();
        await service.SetFeaturedAsync(property.Id, new SetFeaturedRequest(true));

        var request = new UpdatePropertyRequest
        {
            Title = property.Title,
            Type = property.Type,
            Operation = property.Operation,
            Status = PropertyStatus.Paused,
            Price = property.Price,
            City = property.City,
            State = property.State
        };
        var updated = await service.UpdateAsync(property.Id, request);

        Assert.False(updated.IsFeatured);
        Assert.Null(property.FeaturedUntil);
    }

    [Fact]
    public async Task APropertyOfAnotherAgency_IsNotFound_AndAClientCannotFeature()
    {
        Subscribe(_company.Id, FeaturedPackage(3));
        var other = _agencies.Add("Otra");
        var theirs = AddProperty(other.Id);
        var mine = AddProperty();

        await Assert.ThrowsAsync<NotFoundException>(() => CompanyService().SetFeaturedAsync(Guid.CreateVersion7(), new SetFeaturedRequest(true)));
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService(FakeCurrentUser.Client()).SetFeaturedAsync(mine.Id, new SetFeaturedRequest(true)));
        Assert.False(theirs.IsFeaturedAt(_clock.Now));
    }

    [Fact]
    public async Task ThePlatformAdministrator_FeaturesWithThePackageOfThePropertysAgency()
    {
        Subscribe(_company.Id, FeaturedPackage(3));
        var property = AddProperty();

        var dto = await CreateService(FakeCurrentUser.PlatformAdmin()).SetFeaturedAsync(property.Id, new SetFeaturedRequest(true));

        Assert.True(dto.IsFeatured);
    }
}
