using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Billing;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.UnitTests.Domain;
using Alhecasa.UnitTests.Support;
using FluentValidation;

namespace Alhecasa.UnitTests.Application;

public sealed class PlanServiceTests
{
    private readonly FakePlanRepository _plans = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    private PlanService CreateService(ICurrentUser? user = null) =>
        new(_plans, _unitOfWork, user ?? FakeCurrentUser.PlatformAdmin(), new SavePlanRequestValidator(), _clock);

    private static SavePlanRequest Request(string name = "Inmobiliaria 100") => new()
    {
        Name = name,
        Kind = PlanKind.Listings,
        Audience = PlanAudience.Company,
        Limit = 100,
        Period = BillingPeriod.Month,
        Price = 799m
    };

    private static SavePlanRequest FreeRequest(string name = "Gratuito") => new()
    {
        Name = name,
        Kind = PlanKind.Listings,
        Audience = PlanAudience.Individual,
        Limit = 3,
        Price = 0m
    };

    [Fact]
    public async Task OnlyThePlatformAdministrator_ManagesPlans()
    {
        var agencyAdmin = CreateService(FakeCurrentUser.AgencyAdmin(Guid.CreateVersion7()));
        var existing = _plans.Add(PlanTests.Paid());

        await Assert.ThrowsAsync<ForbiddenException>(() => agencyAdmin.ListAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => agencyAdmin.GetByIdAsync(existing.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => agencyAdmin.CreateAsync(Request()));
        await Assert.ThrowsAsync<ForbiddenException>(() => agencyAdmin.UpdateAsync(existing.Id, Request()));
        await Assert.ThrowsAsync<ForbiddenException>(() => agencyAdmin.DeleteAsync(existing.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService(FakeCurrentUser.Client()).ListAsync());
    }

    [Fact]
    public async Task Create_SavesThePlan()
    {
        var created = await CreateService().CreateAsync(Request());

        Assert.Equal("Inmobiliaria 100", created.Name);
        Assert.Equal(100, created.Limit);
        Assert.Equal(BillingPeriod.Month, created.Period);
        Assert.True(created.IsActive);
        Assert.False(created.IsFree);
        Assert.Single(_plans.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_ValidatesTheRequest()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Request(name: "")));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Request(name: new string('x', 101))));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Request() with { Limit = 0 }));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Request() with { Price = -1m }));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Request() with { Kind = (PlanKind)9 }));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Request() with { Description = new string('x', 501) }));
        Assert.Empty(_plans.Items);
    }

    [Fact]
    public async Task Create_AppliesTheDomainRules()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(Request() with { Period = null }));
        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(FreeRequest() with { Audience = PlanAudience.Company }));
        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(FreeRequest() with { Limit = null }));
    }

    [Fact]
    public async Task TheName_MustBeUnique()
    {
        var service = CreateService();
        await service.CreateAsync(Request());

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(Request()));
    }

    [Fact]
    public async Task OnlyOneFreePlan_CanBeActive()
    {
        var service = CreateService();
        await service.CreateAsync(FreeRequest());

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(FreeRequest("Otro gratuito")));
        Assert.Contains("Gratuito", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APaidPlan_CannotBecomeAnotherFreePlan()
    {
        var service = CreateService();
        var free = await service.CreateAsync(FreeRequest());
        var paid = await service.CreateAsync(Request());

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(paid.Id, FreeRequest("Otro")));
        Assert.Equal(free.Id, (await _plans.GetActiveFreeAsync())!.Id);
    }

    [Fact]
    public async Task Update_ChangesThePlan_AndKeepsItsOwnNameAvailable()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Request());

        var updated = await service.UpdateAsync(created.Id, Request() with { Limit = 200, Price = 1_399m });

        Assert.Equal(200, updated.Limit);
        Assert.Equal(1_399m, updated.Price);
        Assert.Equal(created.Id, updated.Id);
    }

    [Fact]
    public async Task TheFreePlanLimit_CanBeEdited_ButItCannotBecomePaid()
    {
        var service = CreateService();
        var free = await service.CreateAsync(FreeRequest());

        var edited = await service.UpdateAsync(free.Id, FreeRequest() with { Limit = 5 });
        Assert.Equal(5, edited.Limit);
        Assert.True(edited.IsFree);

        await Assert.ThrowsAsync<DomainException>(() => service.UpdateAsync(free.Id, Request("Gratuito")));
        await Assert.ThrowsAsync<DomainException>(() => service.UpdateAsync(free.Id, FreeRequest() with { IsActive = false }));
    }

    [Fact]
    public async Task APlan_CanBeDeactivated_AndReactivated()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Request());

        Assert.False((await service.UpdateAsync(created.Id, Request() with { IsActive = false })).IsActive);
        Assert.True((await service.UpdateAsync(created.Id, Request() with { IsActive = true })).IsActive);
    }

    [Fact]
    public async Task Delete_IsLogical_AndTheFreePlanCannotBeDeleted()
    {
        var service = CreateService();
        var free = await service.CreateAsync(FreeRequest());
        var paid = await service.CreateAsync(Request());

        await service.DeleteAsync(paid.Id);

        Assert.True(_plans.Items.Single(p => p.Id == paid.Id).IsDeleted);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(paid.Id));
        await Assert.ThrowsAsync<DomainException>(() => service.DeleteAsync(free.Id));
        Assert.Equal([free.Id], (await service.ListAsync()).Select(p => p.Id));
    }

    [Fact]
    public async Task ADeletedPlan_FreesItsName()
    {
        var service = CreateService();
        var paid = await service.CreateAsync(Request());
        await service.DeleteAsync(paid.Id);

        await service.CreateAsync(Request());

        Assert.Equal(2, _plans.Items.Count);
    }

    [Fact]
    public async Task List_CanFilterByKind()
    {
        var service = CreateService();
        await service.CreateAsync(Request());
        await service.CreateAsync(Request("Destacados") with { Kind = PlanKind.Featured, Limit = 5, Period = BillingPeriod.Week, Audience = PlanAudience.Any });

        Assert.Equal(2, (await service.ListAsync()).Count);
        Assert.Equal(["Destacados"], (await service.ListAsync(PlanKind.Featured)).Select(p => p.Name));
        await Assert.ThrowsAsync<DomainException>(() => service.ListAsync((PlanKind)9));
    }

    [Fact]
    public async Task AnUnknownPlan_IsNotFound()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(Guid.CreateVersion7()));
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(Guid.CreateVersion7(), Request()));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(Guid.CreateVersion7()));
    }
}

public sealed class BillingServiceTests
{
    private readonly FakePlanRepository _plans = new();
    private readonly FakeSubscriptionRepository _subscriptions = new();
    private readonly FakeAgencyRepository _agencies = new();
    private readonly FakePropertyRepository _properties = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly AccountLimits _limits = new(3);

    private EntitlementService CreateEntitlements() => new(_subscriptions, _plans, _properties, _clock, _limits);

    private BillingService CreateService(ICurrentUser? user = null) => new(
        _subscriptions,
        _plans,
        _agencies,
        _properties,
        CreateEntitlements(),
        _unitOfWork,
        user ?? FakeCurrentUser.PlatformAdmin(),
        new AssignPlanRequestValidator(),
        new CheckoutRequestValidator(),
        new FakePaymentGateway(),
        _clock,
        _limits);

    private Plan Listings(int? limit = 100, string name = "Inmobiliaria 100") =>
        _plans.Add(PlanTests.Paid(limit: limit) with { Name = name });

    private Plan Featured(int? limit = 3, string name = "3 destacados", BillingPeriod period = BillingPeriod.Week) =>
        _plans.Add(PlanTests.Paid(PlanKind.Featured, limit, period, 199m) with { Name = name, Audience = PlanAudience.Any });

    private Property AddProperty(Guid agencyId, PropertyStatus status = PropertyStatus.Published)
    {
        var property = new Property(agencyId, PropertyTests.ValidData() with { Status = status });
        _properties.Items.Add(property);
        return property;
    }

    // --- Quién puede ----------------------------------------------------------------------

    [Fact]
    public async Task OnlyThePlatformAdministrator_ManagesSubscriptions()
    {
        var agency = _agencies.Add();
        var plan = Listings();
        var staff = CreateService(FakeCurrentUser.AgencyAdmin(agency.Id));

        await Assert.ThrowsAsync<ForbiddenException>(() => staff.ListAccountsAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => staff.ListSubscriptionsAsync(agency.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => staff.AssignAsync(agency.Id, new AssignPlanRequest(plan.Id)));
        await Assert.ThrowsAsync<ForbiddenException>(() => staff.CancelAsync(agency.Id, Guid.CreateVersion7()));
    }

    [Fact]
    public async Task TheAccountPlan_IsOnlyForAgencyStaff()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService(FakeCurrentUser.PlatformAdmin()).GetMineAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService(FakeCurrentUser.Client()).GetMineAsync());
    }

    // --- Contratar ------------------------------------------------------------------------

    [Fact]
    public async Task Assign_StartsNow_AndLastsOnePeriod()
    {
        var agency = _agencies.Add();
        var plan = Listings();

        var dto = await CreateService().AssignAsync(agency.Id, new AssignPlanRequest(plan.Id, PaymentReference: "SPEI 99"));

        Assert.Equal(_clock.Now, dto.StartsAt);
        Assert.Equal(_clock.Now.AddMonths(1), dto.EndsAt);
        Assert.Equal(799m, dto.AmountPaid);
        Assert.Equal("SPEI 99", dto.PaymentReference);
        Assert.Equal(100, dto.Limit);
        Assert.Equal(PlanKind.Listings, dto.Kind);
        Assert.True(dto.IsActive);
        Assert.Single(_subscriptions.Items);
    }

    [Fact]
    public async Task Assign_RecordsWhatWasActuallyPaid_WhenItDiffersFromThePrice()
    {
        var agency = _agencies.Add();
        var plan = Listings();

        var dto = await CreateService().AssignAsync(agency.Id, new AssignPlanRequest(plan.Id, AmountPaid: 500m));

        Assert.Equal(500m, dto.AmountPaid);
    }

    [Fact]
    public async Task Assign_RejectsAnInactiveAFreeOrAnUnknownPlan_AndAnUnknownAgency()
    {
        var agency = _agencies.Add();
        var inactive = _plans.Add(PlanTests.Paid() with { Name = "Inactivo", IsActive = false });
        var free = _plans.Add(PlanTests.Free());
        var service = CreateService();

        await Assert.ThrowsAsync<DomainException>(() => service.AssignAsync(agency.Id, new AssignPlanRequest(inactive.Id)));
        await Assert.ThrowsAsync<DomainException>(() => service.AssignAsync(agency.Id, new AssignPlanRequest(free.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => service.AssignAsync(agency.Id, new AssignPlanRequest(Guid.CreateVersion7())));
        await Assert.ThrowsAsync<NotFoundException>(() => service.AssignAsync(Guid.CreateVersion7(), new AssignPlanRequest(Listings(name: "Otro").Id)));
        Assert.Empty(_subscriptions.Items);
    }

    [Fact]
    public async Task Assign_ValidatesTheRequest()
    {
        var agency = _agencies.Add();
        var plan = Listings();
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => service.AssignAsync(agency.Id, new AssignPlanRequest(Guid.Empty)));
        await Assert.ThrowsAsync<ValidationException>(() => service.AssignAsync(agency.Id, new AssignPlanRequest(plan.Id, AmountPaid: -1m)));
        await Assert.ThrowsAsync<ValidationException>(() => service.AssignAsync(agency.Id, new AssignPlanRequest(plan.Id, PaymentReference: new string('x', 201))));
    }

    [Fact]
    public async Task Assign_ChecksThePlanAudience()
    {
        var company = _agencies.Add();
        var individual = _agencies.AddIndividual();
        var forCompanies = Listings();
        var forAnyone = Featured();
        var service = CreateService();

        await Assert.ThrowsAsync<DomainException>(() => service.AssignAsync(individual.Id, new AssignPlanRequest(forCompanies.Id)));
        await service.AssignAsync(individual.Id, new AssignPlanRequest(forAnyone.Id));
        await service.AssignAsync(company.Id, new AssignPlanRequest(forAnyone.Id));
        await service.AssignAsync(company.Id, new AssignPlanRequest(forCompanies.Id));
    }

    [Fact]
    public async Task Assign_ReplacesTheActiveSubscriptionOfTheSameKind_ButNotTheOtherKind()
    {
        var agency = _agencies.Add();
        var service = CreateService();
        var first = await service.AssignAsync(agency.Id, new AssignPlanRequest(Listings().Id));
        var featured = await service.AssignAsync(agency.Id, new AssignPlanRequest(Featured().Id));

        await service.AssignAsync(agency.Id, new AssignPlanRequest(Listings(200, "Inmobiliaria 200").Id));

        Assert.NotNull(_subscriptions.Items.Single(s => s.Id == first.Id).CancelledAt);
        Assert.Null(_subscriptions.Items.Single(s => s.Id == featured.Id).CancelledAt);
        var mine = await CreateService(FakeCurrentUser.AgencyAdmin(agency.Id)).GetMineAsync();
        Assert.Equal(200, mine.Listings.Limit);
        Assert.Equal("3 destacados", mine.Featured!.PlanName);
    }

    [Fact]
    public async Task ChangingTheFeaturedPackage_ClearsTheFeaturedAds()
    {
        var agency = _agencies.Add();
        var service = CreateService();
        await service.AssignAsync(agency.Id, new AssignPlanRequest(Featured().Id));
        var property = AddProperty(agency.Id);
        property.Feature(_clock.Now.AddDays(7), _clock.Now);

        await service.AssignAsync(agency.Id, new AssignPlanRequest(Featured(10, "10 destacados").Id));

        Assert.False(property.IsFeaturedAt(_clock.Now));
    }

    [Fact]
    public async Task ChangingTheListingsPlan_KeepsTheFeaturedAds()
    {
        var agency = _agencies.Add();
        var service = CreateService();
        await service.AssignAsync(agency.Id, new AssignPlanRequest(Featured().Id));
        var property = AddProperty(agency.Id);
        property.Feature(_clock.Now.AddDays(7), _clock.Now);

        await service.AssignAsync(agency.Id, new AssignPlanRequest(Listings().Id));

        Assert.True(property.IsFeaturedAt(_clock.Now));
    }

    // --- Cancelar -------------------------------------------------------------------------

    [Fact]
    public async Task Cancel_EndsTheSubscriptionNow()
    {
        var agency = _agencies.Add();
        var service = CreateService();
        var created = await service.AssignAsync(agency.Id, new AssignPlanRequest(Listings().Id));
        _clock.Advance(TimeSpan.FromDays(2));

        var cancelled = await service.CancelAsync(agency.Id, created.Id);

        Assert.False(cancelled.IsActive);
        Assert.Equal(_clock.Now, cancelled.CancelledAt);
        Assert.Null((await CreateService(FakeCurrentUser.AgencyAdmin(agency.Id)).GetMineAsync()).Listings.PlanName);
    }

    [Fact]
    public async Task CancellingAFeaturedPackage_ClearsTheFeaturedAds()
    {
        var agency = _agencies.Add();
        var service = CreateService();
        var created = await service.AssignAsync(agency.Id, new AssignPlanRequest(Featured().Id));
        var property = AddProperty(agency.Id);
        property.Feature(_clock.Now.AddDays(7), _clock.Now);

        await service.CancelAsync(agency.Id, created.Id);

        Assert.False(property.IsFeaturedAt(_clock.Now));
    }

    [Fact]
    public async Task Cancel_OfASubscriptionFromAnotherAccount_IsNotFound()
    {
        var agency = _agencies.Add();
        var other = _agencies.Add("Otra");
        var created = await CreateService().AssignAsync(agency.Id, new AssignPlanRequest(Listings().Id));

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().CancelAsync(other.Id, created.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().CancelAsync(agency.Id, Guid.CreateVersion7()));
    }

    // --- Historial y cuenta ---------------------------------------------------------------

    [Fact]
    public async Task ListSubscriptions_ShowsTheHistory_NewestFirst_AndMarksWhichOnesAreActive()
    {
        var agency = _agencies.Add();
        var service = CreateService();
        await service.AssignAsync(agency.Id, new AssignPlanRequest(Listings().Id));
        _clock.Advance(TimeSpan.FromDays(1));
        await service.AssignAsync(agency.Id, new AssignPlanRequest(Listings(200, "Inmobiliaria 200").Id));

        var list = await service.ListSubscriptionsAsync(agency.Id);

        Assert.Equal([200, 100], list.Select(s => s.Limit));
        Assert.Equal([true, false], list.Select(s => s.IsActive));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ListSubscriptionsAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task AnExpiredSubscription_NoLongerCounts()
    {
        var agency = _agencies.Add();
        await CreateService().AssignAsync(agency.Id, new AssignPlanRequest(Listings().Id));
        _clock.Advance(TimeSpan.FromDays(32));

        var mine = await CreateService(FakeCurrentUser.AgencyAdmin(agency.Id)).GetMineAsync();

        Assert.Null(mine.Listings.PlanName);
        Assert.Null(mine.Listings.Limit);
    }

    [Fact]
    public async Task AnAgencyWithoutAPlan_HasNoLimit()
    {
        var agency = _agencies.Add();
        AddProperty(agency.Id);

        var mine = await CreateService(FakeCurrentUser.AgencyAdmin(agency.Id)).GetMineAsync();

        Assert.Null(mine.Listings.PlanName);
        Assert.Null(mine.Listings.Limit);
        Assert.Equal(1, mine.Listings.Used);
        Assert.False(mine.Listings.IsFree);
        Assert.Null(mine.Featured);
        Assert.False(mine.IsIndividual);
    }

    [Fact]
    public async Task AnIndividual_HasTheFreePlan_OrTheConfiguredLimitIfThereIsNone()
    {
        var individual = _agencies.AddIndividual();
        var service = CreateService(FakeCurrentUser.AgencyAdmin(individual.Id));

        var fallback = (await service.GetMineAsync()).Listings;
        Assert.Equal(3, fallback.Limit);
        Assert.True(fallback.IsFree);

        _plans.Add(PlanTests.Free(limit: 5));
        var free = (await service.GetMineAsync()).Listings;
        Assert.Equal(5, free.Limit);
        Assert.Equal("Gratuito", free.PlanName);
        Assert.Null(free.EndsAt);
    }

    [Fact]
    public async Task APaidPlan_ReplacesTheFreeOneForAnIndividual()
    {
        var individual = _agencies.AddIndividual();
        _plans.Add(PlanTests.Free());
        var plan = _plans.Add(PlanTests.Paid(limit: 10) with { Name = "Particular 10", Audience = PlanAudience.Individual });
        await CreateService().AssignAsync(individual.Id, new AssignPlanRequest(plan.Id));

        var listings = (await CreateService(FakeCurrentUser.AgencyAdmin(individual.Id)).GetMineAsync()).Listings;

        Assert.Equal(10, listings.Limit);
        Assert.Equal("Particular 10", listings.PlanName);
        Assert.False(listings.IsFree);
        Assert.NotNull(listings.EndsAt);
    }

    [Fact]
    public async Task ListAccounts_ShowsEveryAccountWithItsPlanAndUsage()
    {
        _plans.Add(PlanTests.Free());
        var company = _agencies.Add("Inmobiliaria");
        var individual = _agencies.AddIndividual();
        var service = CreateService();
        await service.AssignAsync(company.Id, new AssignPlanRequest(Listings(300, "Inmobiliaria 300").Id));
        await service.AssignAsync(company.Id, new AssignPlanRequest(Featured(5, "5 destacados").Id));
        AddProperty(company.Id);
        AddProperty(company.Id).Feature(_clock.Now.AddDays(3), _clock.Now);
        AddProperty(individual.Id, PropertyStatus.Draft);

        var accounts = await service.ListAccountsAsync();

        var c = accounts.Single(a => a.AgencyId == company.Id);
        Assert.Equal("Inmobiliaria 300", c.Listings.PlanName);
        Assert.Equal(300, c.Listings.Limit);
        Assert.Equal(2, c.Listings.Used);
        Assert.Equal("5 destacados", c.Featured!.PlanName);
        Assert.Equal(1, c.Featured.Used);
        Assert.Equal(5, c.Featured.Limit);

        var i = accounts.Single(a => a.AgencyId == individual.Id);
        Assert.True(i.IsIndividual);
        Assert.Equal("Gratuito", i.Listings.PlanName);
        Assert.Equal(3, i.Listings.Limit);
        Assert.Equal(1, i.Listings.Used);
        Assert.Null(i.Featured);
    }
}
