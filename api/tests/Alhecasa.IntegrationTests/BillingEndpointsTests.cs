using System.Net;
using Alhecasa.Application.Billing;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.IntegrationTests;

/// <summary>Planes de pago, contrataciones, paquetes de destacados y su efecto en el catálogo.</summary>
public sealed class BillingEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string PlansUrl = "/api/plans";
    private const string BillingUrl = "/api/billing";
    private const string PropertiesUrl = "/api/properties";
    private const string PublicUrl = "/api/public/properties";

    private static SavePlanRequest ListingsPlan(string? name = null, int? limit = 100) => new()
    {
        Name = name ?? $"Inmobiliaria {Guid.NewGuid():N}"[..24],
        Kind = PlanKind.Listings,
        Audience = PlanAudience.Company,
        Limit = limit,
        Period = BillingPeriod.Month,
        Price = 799m
    };

    private static SavePlanRequest FeaturedPlan(string? name = null, int? limit = 3, BillingPeriod period = BillingPeriod.Week) => new()
    {
        Name = name ?? $"Destacados {Guid.NewGuid():N}"[..24],
        Kind = PlanKind.Featured,
        Audience = PlanAudience.Any,
        Limit = limit,
        Period = period,
        Price = 199m
    };

    private async Task<User> PlatformAdminAsync() => await factory.AddUserAsync(UserRole.PlatformAdmin, null);

    private async Task<PlanDto> CreatePlanAsync(SavePlanRequest request)
    {
        var admin = await PlatformAdminAsync();
        var response = await factory.CreateClientFor(admin).PostJsonAsync(PlansUrl, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<PlanDto>();
    }

    private async Task<SubscriptionDto> AssignAsync(Guid agencyId, Guid planId, AssignPlanRequest? request = null)
    {
        var admin = await PlatformAdminAsync();
        var response = await factory.CreateClientFor(admin).PostJsonAsync($"{BillingUrl}/accounts/{agencyId}/subscriptions", request ?? new AssignPlanRequest(planId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<SubscriptionDto>();
    }

    private async Task<Agency> AddIndividualAsync()
    {
        var agency = Agency.CreateIndividual("Particular", $"{Guid.NewGuid():N}@individual.test");
        await factory.WithDbAsync(async db =>
        {
            db.Agencies.Add(agency);
            await db.SaveChangesAsync();
        });
        return agency;
    }

    private static CreatePropertyRequest NewProperty(string title = "Casa de prueba") => new()
    {
        Title = title,
        Type = PropertyType.House,
        Operation = OperationType.Sale,
        Price = 1_000_000m,
        City = "Ciudad de México",
        State = "CDMX"
    };

    private async Task<HttpResponseMessage> SetFeaturedAsync(User user, Guid propertyId, bool featured) =>
        await factory.CreateClientFor(user).PutJsonAsync($"{PropertiesUrl}/{propertyId}/featured", new SetFeaturedRequest(featured));

    // --- Planes ---------------------------------------------------------------------------

    [Fact]
    public async Task Plans_RequireThePlatformAdministrator()
    {
        var agency = await factory.AddAgencyAsync();
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(PlansUrl)).StatusCode);
        foreach (var role in new[] { UserRole.AgencyAdmin, UserRole.Supervisor, UserRole.Seller })
        {
            var client = factory.CreateClientFor(await factory.AddUserAsync(role, agency.Id));
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(PlansUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync(PlansUrl, ListingsPlan())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{PlansUrl}/{Guid.NewGuid()}")).StatusCode);
        }
    }

    [Fact]
    public async Task APlan_CanBeCreatedReadUpdatedAndDeleted()
    {
        var admin = factory.CreateClientFor(await PlatformAdminAsync());
        var name = $"Plan {Guid.NewGuid():N}"[..20];

        var created = await admin.PostJsonAsync(PlansUrl, ListingsPlan(name, 200));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var plan = await created.ReadAsync<PlanDto>();
        Assert.Equal(200, plan.Limit);
        Assert.Equal(BillingPeriod.Month, plan.Period);
        Assert.True(plan.IsActive);

        Assert.Equal(name, (await (await admin.GetAsync($"{PlansUrl}/{plan.Id}")).ReadAsync<PlanDto>()).Name);
        Assert.Contains((await (await admin.GetAsync(PlansUrl)).ReadAsync<List<PlanDto>>()), p => p.Id == plan.Id);

        var updated = await admin.PutJsonAsync($"{PlansUrl}/{plan.Id}", ListingsPlan(name, 300) with { Price = 1_899m, IsActive = false });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var after = await updated.ReadAsync<PlanDto>();
        Assert.Equal(300, after.Limit);
        Assert.Equal(1_899m, after.Price);
        Assert.False(after.IsActive);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{PlansUrl}/{plan.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{PlansUrl}/{plan.Id}")).StatusCode);
        Assert.DoesNotContain((await (await admin.GetAsync(PlansUrl)).ReadAsync<List<PlanDto>>()), p => p.Id == plan.Id);
    }

    [Fact]
    public async Task APlan_CanBeUnlimited_AndTheKindsAreSerializedAsText()
    {
        var admin = factory.CreateClientFor(await PlatformAdminAsync());

        var response = await admin.PostJsonAsync(PlansUrl, FeaturedPlan(limit: null, period: BillingPeriod.Year));
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("\"kind\":\"Featured\"", json, StringComparison.Ordinal);
        Assert.Contains("\"period\":\"Year\"", json, StringComparison.Ordinal);
        Assert.Contains("\"limit\":null", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APlan_CanBeFilteredByKind()
    {
        var admin = factory.CreateClientFor(await PlatformAdminAsync());
        var listings = await CreatePlanAsync(ListingsPlan());
        var featured = await CreatePlanAsync(FeaturedPlan());

        var onlyFeatured = await (await admin.GetAsync($"{PlansUrl}?kind=Featured")).ReadAsync<List<PlanDto>>();

        Assert.Contains(onlyFeatured, p => p.Id == featured.Id);
        Assert.DoesNotContain(onlyFeatured, p => p.Id == listings.Id);
        Assert.All(onlyFeatured, p => Assert.Equal(PlanKind.Featured, p.Kind));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"{PlansUrl}?kind=Nope")).StatusCode);
    }

    [Fact]
    public async Task InvalidPlans_AreRejectedWith400()
    {
        var admin = factory.CreateClientFor(await PlatformAdminAsync());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostJsonAsync(PlansUrl, ListingsPlan() with { Name = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostJsonAsync(PlansUrl, ListingsPlan(limit: 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostJsonAsync(PlansUrl, ListingsPlan() with { Period = null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostJsonAsync(PlansUrl, ListingsPlan() with { Price = -5m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostJsonAsync(PlansUrl, ListingsPlan() with { Price = 0m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync(PlansUrl, new StringContent("""{"name":"x","kind":"Nope"}""", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
    }

    [Fact]
    public async Task ADuplicatedName_Responds409()
    {
        var admin = factory.CreateClientFor(await PlatformAdminAsync());
        var request = ListingsPlan();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostJsonAsync(PlansUrl, request)).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostJsonAsync(PlansUrl, request)).StatusCode);
    }

    [Fact]
    public async Task TheFreePlan_IsUnique_AndCannotBeDeleted()
    {
        await using var isolated = new ApiFactory();
        var admin = isolated.CreateClientFor(await isolated.AddUserAsync(UserRole.PlatformAdmin, null));
        var free = new SavePlanRequest { Name = "Gratuito", Kind = PlanKind.Listings, Audience = PlanAudience.Individual, Limit = 3, Price = 0m };

        var created = await admin.PostJsonAsync(PlansUrl, free);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var plan = await created.ReadAsync<PlanDto>();
        Assert.True(plan.IsFree);
        Assert.Null(plan.Period);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostJsonAsync(PlansUrl, free with { Name = "Otro gratuito" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"{PlansUrl}/{plan.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutJsonAsync($"{PlansUrl}/{plan.Id}", free with { Limit = 5 })).StatusCode);
    }

    // --- Contrataciones -------------------------------------------------------------------

    [Fact]
    public async Task AnAgencyWithoutAPlan_SeesNoLimit_AndAssigningOneSetsIt()
    {
        var agency = await factory.AddAgencyAsync();
        var staff = factory.CreateClientFor(await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id));

        var before = await (await staff.GetAsync($"{BillingUrl}/me")).ReadAsync<AccountBillingDto>();
        Assert.Null(before.Listings.PlanName);
        Assert.Null(before.Listings.Limit);
        Assert.Null(before.Featured);

        var plan = await CreatePlanAsync(ListingsPlan(limit: 100));
        var subscription = await AssignAsync(agency.Id, plan.Id, new AssignPlanRequest(plan.Id, PaymentReference: "SPEI 1"));
        Assert.True(subscription.IsActive);
        Assert.Equal(subscription.StartsAt.AddMonths(1), subscription.EndsAt);

        var after = await (await staff.GetAsync($"{BillingUrl}/me")).ReadAsync<AccountBillingDto>();
        Assert.Equal(plan.Name, after.Listings.PlanName);
        Assert.Equal(100, after.Listings.Limit);
        Assert.True((subscription.EndsAt - after.Listings.EndsAt!.Value).Duration() < TimeSpan.FromSeconds(1)); // SQLite guarda menos precisión que SQL Server
    }

    [Fact]
    public async Task OnlyThePlatformAdministrator_ManagesSubscriptions_AndStaffOnlyReadTheirOwnPlan()
    {
        var agency = await factory.AddAgencyAsync();
        var plan = await CreatePlanAsync(ListingsPlan());
        var seller = factory.CreateClientFor(await factory.AddUserAsync(UserRole.Seller, agency.Id));
        var admin = factory.CreateClientFor(await PlatformAdminAsync());
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{BillingUrl}/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync($"{BillingUrl}/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync($"{BillingUrl}/accounts/{agency.Id}/subscriptions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.PostJsonAsync($"{BillingUrl}/accounts/{agency.Id}/subscriptions", new AssignPlanRequest(plan.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.PostAsync($"{BillingUrl}/accounts/{agency.Id}/subscriptions/{Guid.NewGuid()}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await seller.GetAsync($"{BillingUrl}/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"{BillingUrl}/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.CreateClientFor(await factory.AddUserAsync(UserRole.Client, null)).GetAsync($"{BillingUrl}/me")).StatusCode);
    }

    [Fact]
    public async Task Assigning_ValidatesThePlanAndTheAccount()
    {
        var company = await factory.AddAgencyAsync();
        var individual = await AddIndividualAsync();
        var admin = factory.CreateClientFor(await PlatformAdminAsync());
        var forCompanies = await CreatePlanAsync(ListingsPlan());
        var inactive = await CreatePlanAsync(ListingsPlan() with { IsActive = false });
        var url = $"{BillingUrl}/accounts/{company.Id}/subscriptions";

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostJsonAsync(url, new AssignPlanRequest(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostJsonAsync($"{BillingUrl}/accounts/{Guid.NewGuid()}/subscriptions", new AssignPlanRequest(forCompanies.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostJsonAsync(url, new AssignPlanRequest(inactive.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostJsonAsync(url, new AssignPlanRequest(Guid.Empty))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostJsonAsync(url, new AssignPlanRequest(forCompanies.Id, AmountPaid: -1m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostJsonAsync($"{BillingUrl}/accounts/{individual.Id}/subscriptions", new AssignPlanRequest(forCompanies.Id))).StatusCode);
    }

    [Fact]
    public async Task TheHistory_ListsEverySubscription_AndCancellingEndsOne()
    {
        var agency = await factory.AddAgencyAsync();
        var admin = factory.CreateClientFor(await PlatformAdminAsync());
        var first = await AssignAsync(agency.Id, (await CreatePlanAsync(ListingsPlan(limit: 100))).Id);
        var second = await AssignAsync(agency.Id, (await CreatePlanAsync(ListingsPlan(limit: 200))).Id);

        var history = await (await admin.GetAsync($"{BillingUrl}/accounts/{agency.Id}/subscriptions")).ReadAsync<List<SubscriptionDto>>();
        Assert.Equal([second.Id, first.Id], history.Select(s => s.Id));
        Assert.Equal([true, false], history.Select(s => s.IsActive));

        var cancelled = await admin.PostAsync($"{BillingUrl}/accounts/{agency.Id}/subscriptions/{second.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.False((await cancelled.ReadAsync<SubscriptionDto>()).IsActive);

        var staff = factory.CreateClientFor(await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id));
        Assert.Null((await (await staff.GetAsync($"{BillingUrl}/me")).ReadAsync<AccountBillingDto>()).Listings.PlanName);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"{BillingUrl}/accounts/{agency.Id}/subscriptions/{Guid.NewGuid()}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{BillingUrl}/accounts/{Guid.NewGuid()}/subscriptions")).StatusCode);
    }

    [Fact]
    public async Task AnAgency_CannotSeeAnotherAgencysPlan()
    {
        var a = await factory.AddAgencyAsync("A");
        var b = await factory.AddAgencyAsync("B");
        await AssignAsync(a.Id, (await CreatePlanAsync(ListingsPlan(limit: 100))).Id);
        var staffB = factory.CreateClientFor(await factory.AddUserAsync(UserRole.AgencyAdmin, b.Id));

        var mine = await (await staffB.GetAsync($"{BillingUrl}/me")).ReadAsync<AccountBillingDto>();

        Assert.Equal(b.Id, mine.AgencyId);
        Assert.Null(mine.Listings.PlanName);
    }

    [Fact]
    public async Task TheAccountsBoard_ShowsEveryAccountWithItsUsage()
    {
        var agency = await factory.AddAgencyAsync("Con plan");
        var plan = await CreatePlanAsync(ListingsPlan(limit: 50));
        await AssignAsync(agency.Id, plan.Id);
        await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);
        await factory.AddPropertyAsync(agency.Id);
        var admin = factory.CreateClientFor(await PlatformAdminAsync());

        var accounts = await (await admin.GetAsync($"{BillingUrl}/accounts")).ReadAsync<List<AccountBillingDto>>();

        var row = accounts.Single(a => a.AgencyId == agency.Id);
        Assert.Equal(plan.Name, row.Listings.PlanName);
        Assert.Equal(50, row.Listings.Limit);
        Assert.Equal(2, row.Listings.Used);
    }

    // --- Tope de publicaciones ------------------------------------------------------------

    [Fact]
    public async Task AnAgencyWithAPlan_IsBlockedWith422OnceItReachesTheLimit()
    {
        var agency = await factory.AddAgencyAsync();
        await AssignAsync(agency.Id, (await CreatePlanAsync(ListingsPlan(limit: 2))).Id);
        var staff = factory.CreateClientFor(await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id));

        Assert.Equal(HttpStatusCode.Created, (await staff.PostJsonAsync(PropertiesUrl, NewProperty("Una"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await staff.PostJsonAsync(PropertiesUrl, NewProperty("Dos"))).StatusCode);
        var blocked = await staff.PostJsonAsync(PropertiesUrl, NewProperty("Tres"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        Assert.Contains("contrata un plan mayor", (await blocked.ReadProblemAsync()).Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnIndividual_HasTheDefaultLimit_UntilItSubscribesToAPlanForIndividuals()
    {
        var individual = await AddIndividualAsync();
        var staff = factory.CreateClientFor(await factory.AddUserAsync(UserRole.AgencyAdmin, individual.Id));
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await staff.PostJsonAsync(PropertiesUrl, NewProperty($"Anuncio {i}"))).StatusCode);
        }

        var blocked = await staff.PostJsonAsync(PropertiesUrl, NewProperty("Cuarto"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        Assert.Contains("cuenta gratuita", (await blocked.ReadProblemAsync()).Detail, StringComparison.Ordinal);

        var plan = await CreatePlanAsync(ListingsPlan(limit: 10) with { Audience = PlanAudience.Individual });
        await AssignAsync(individual.Id, plan.Id);

        Assert.Equal(HttpStatusCode.Created, (await staff.PostJsonAsync(PropertiesUrl, NewProperty("Cuarto"))).StatusCode);
        var mine = await (await staff.GetAsync($"{BillingUrl}/me")).ReadAsync<AccountBillingDto>();
        Assert.Equal(10, mine.Listings.Limit);
        Assert.Equal(4, mine.Listings.Used);
        Assert.True(mine.IsIndividual);
    }

    // --- Destacados -----------------------------------------------------------------------

    [Fact]
    public async Task Featuring_NeedsAPackage_AFreeSlot_AndAPublishedProperty()
    {
        var agency = await factory.AddAgencyAsync();
        var user = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        var one = await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);
        var two = await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);
        var draft = await factory.AddPropertyAsync(agency.Id);

        var noPackage = await SetFeaturedAsync(user, one.Id, true);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noPackage.StatusCode);
        Assert.Contains("paquete de destacados", (await noPackage.ReadProblemAsync()).Detail, StringComparison.Ordinal);

        var package = await CreatePlanAsync(FeaturedPlan(limit: 1));
        var subscription = await AssignAsync(agency.Id, package.Id);

        var featured = await SetFeaturedAsync(user, one.Id, true);
        Assert.Equal(HttpStatusCode.OK, featured.StatusCode);
        var dto = await featured.ReadAsync<PropertyDto>();
        Assert.True(dto.IsFeatured);
        Assert.True((subscription.EndsAt - dto.FeaturedUntil!.Value).Duration() < TimeSpan.FromSeconds(1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SetFeaturedAsync(user, two.Id, true)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SetFeaturedAsync(user, draft.Id, true)).StatusCode);

        var mine = await (await factory.CreateClientFor(user).GetAsync($"{BillingUrl}/me")).ReadAsync<AccountBillingDto>();
        Assert.Equal(1, mine.Featured!.Used);
        Assert.Equal(1, mine.Featured.Limit);

        Assert.Equal(HttpStatusCode.OK, (await SetFeaturedAsync(user, one.Id, false)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SetFeaturedAsync(user, two.Id, true)).StatusCode);
    }

    [Fact]
    public async Task TheStaffList_ShowsWhichPropertiesAreFeatured()
    {
        var agency = await factory.AddAgencyAsync();
        var user = await factory.AddUserAsync(UserRole.Seller, agency.Id);
        await AssignAsync(agency.Id, (await CreatePlanAsync(FeaturedPlan())).Id);
        var property = await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);
        await SetFeaturedAsync(user, property.Id, true);

        var page = await (await factory.CreateClientFor(user).GetAsync(PropertiesUrl)).ReadAsync<PagedResult<PropertyDto>>();

        Assert.True(Assert.Single(page.Items).IsFeatured);
    }

    [Fact]
    public async Task APropertyOfAnotherAgency_CannotBeFeatured()
    {
        var mine = await factory.AddAgencyAsync();
        var theirs = await factory.AddAgencyAsync();
        var user = await factory.AddUserAsync(UserRole.AgencyAdmin, mine.Id);
        await AssignAsync(mine.Id, (await CreatePlanAsync(FeaturedPlan())).Id);
        var other = await factory.AddPropertyAsync(theirs.Id, status: PropertyStatus.Published);

        Assert.Equal(HttpStatusCode.NotFound, (await SetFeaturedAsync(user, other.Id, true)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PutJsonAsync($"{PropertiesUrl}/{other.Id}/featured", new SetFeaturedRequest(true))).StatusCode);
    }

    [Fact]
    public async Task ChangingThePackage_ClearsTheFeaturedAds()
    {
        var agency = await factory.AddAgencyAsync();
        var user = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        await AssignAsync(agency.Id, (await CreatePlanAsync(FeaturedPlan(limit: 3))).Id);
        var property = await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);
        await SetFeaturedAsync(user, property.Id, true);

        await AssignAsync(agency.Id, (await CreatePlanAsync(FeaturedPlan(limit: 10))).Id);

        var dto = await (await factory.CreateClientFor(user).GetAsync($"{PropertiesUrl}/{property.Id}")).ReadAsync<PropertyDto>();
        Assert.False(dto.IsFeatured);
    }

    // --- Catálogo público -----------------------------------------------------------------

    [Fact]
    public async Task FeaturedProperties_GoFirstInTheDefaultOrder_ButNotWhenAnotherOrderIsAsked()
    {
        var agency = await factory.AddAgencyAsync();
        var user = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        await AssignAsync(agency.Id, (await CreatePlanAsync(FeaturedPlan(limit: 5))).Id);
        var city = $"Ciudad{Guid.NewGuid():N}"[..16];

        async Task<Property> AddAsync(string title, decimal price)
        {
            var data = new PropertyData(title, null, PropertyType.House, OperationType.Sale, PropertyStatus.Published, price, CurrencyCode.MXN,
                null, null, city, "Estado", null, null, null, null, null);
            return await factory.AddPropertyAsync(agency.Id, data);
        }

        var oldest = await AddAsync("Más antigua y cara", 9_000_000m);
        await Task.Delay(20);
        var middle = await AddAsync("Intermedia", 5_000_000m);
        await Task.Delay(20);
        await AddAsync("Más reciente y barata", 1_000_000m);
        await SetFeaturedAsync(user, oldest.Id, true);

        async Task<List<PublicPropertyDto>> ListAsync(string query) =>
            (await (await factory.CreateClient().GetAsync($"{PublicUrl}?location={city}{query}")).ReadAsync<PagedResult<PublicPropertyDto>>()).Items.ToList();

        var recent = await ListAsync("");
        Assert.Equal(["Más antigua y cara", "Más reciente y barata", "Intermedia"], recent.Select(p => p.Title));
        Assert.Equal([true, false, false], recent.Select(p => p.IsFeatured));

        var cheapFirst = await ListAsync("&sort=PriceAsc");
        Assert.Equal(["Más reciente y barata", "Intermedia", "Más antigua y cara"], cheapFirst.Select(p => p.Title));
        Assert.True(cheapFirst.Single(p => p.Title == "Más antigua y cara").IsFeatured);

        var detail = await (await factory.CreateClient().GetAsync($"{PublicUrl}/{oldest.Id}")).ReadAsync<PublicPropertyDto>();
        Assert.True(detail.IsFeatured);
        Assert.False((await (await factory.CreateClient().GetAsync($"{PublicUrl}/{middle.Id}")).ReadAsync<PublicPropertyDto>()).IsFeatured);
    }

    [Fact]
    public async Task AnExpiredFeature_NoLongerShowsNorPinsTheProperty()
    {
        var agency = await factory.AddAgencyAsync();
        var city = $"Ciudad{Guid.NewGuid():N}"[..16];
        PropertyData Data(string title) => new(title, null, PropertyType.House, OperationType.Sale, PropertyStatus.Published, 1_000_000m, CurrencyCode.MXN,
            null, null, city, "Estado", null, null, null, null, null);
        var old = await factory.AddPropertyAsync(agency.Id, Data("Vieja"));
        await Task.Delay(20);
        await factory.AddPropertyAsync(agency.Id, Data("Nueva"));
        await factory.WithDbAsync(async db =>
        {
            var property = await db.Properties.IgnoreQueryFilters().SingleAsync(p => p.Id == old.Id);
            db.Entry(property).Property(nameof(Property.FeaturedUntil)).CurrentValue = DateTimeOffset.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        });

        var items = (await (await factory.CreateClient().GetAsync($"{PublicUrl}?location={city}")).ReadAsync<PagedResult<PublicPropertyDto>>()).Items;

        Assert.Equal(["Nueva", "Vieja"], items.Select(p => p.Title));
        Assert.All(items, p => Assert.False(p.IsFeatured));
    }
}
