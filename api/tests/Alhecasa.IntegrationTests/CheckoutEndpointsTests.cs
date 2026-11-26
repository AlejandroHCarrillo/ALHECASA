using System.Net;
using Alhecasa.Application.Billing;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;

namespace Alhecasa.IntegrationTests;

/// <summary>El administrador de la inmobiliaria compra planes con tarjeta (pasarela simulada).</summary>
public sealed class CheckoutEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string BillingUrl = "/api/billing";
    private const string GoodCard = "4242 4242 4242 4242";

    private static PaymentCardRequest Card(string number = GoodCard, int month = 12, int year = 2099, string cvv = "123") =>
        new(number, "ANA LOPEZ", month, year, cvv);

    private async Task<PlanDto> CreatePlanAsync(PlanKind kind = PlanKind.Listings, PlanAudience audience = PlanAudience.Company, bool active = true)
    {
        var admin = await factory.AddUserAsync(UserRole.PlatformAdmin, null);
        var response = await factory.CreateClientFor(admin).PostJsonAsync("/api/plans", new SavePlanRequest
        {
            Name = $"Plan {Guid.NewGuid():N}"[..20],
            Kind = kind,
            Audience = audience,
            Limit = kind == PlanKind.Listings ? 200 : 5,
            Period = kind == PlanKind.Listings ? BillingPeriod.Month : BillingPeriod.Week,
            Price = 1399m,
            IsActive = active
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<PlanDto>();
    }

    private async Task<HttpClient> AdminOfAsync(Agency agency) =>
        factory.CreateClientFor(await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id));

    [Fact]
    public async Task TheCatalogAndTheCheckout_RequireTheAccountAdministrator()
    {
        var agency = await factory.AddAgencyAsync();
        var plan = await CreatePlanAsync();
        var body = new CheckoutRequest(plan.Id, Card());

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"{BillingUrl}/plans")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostJsonAsync($"{BillingUrl}/checkout", body)).StatusCode);

        foreach (var role in new[] { UserRole.Supervisor, UserRole.Seller })
        {
            var staff = factory.CreateClientFor(await factory.AddUserAsync(role, agency.Id));
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync($"{BillingUrl}/plans")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostJsonAsync($"{BillingUrl}/checkout", body)).StatusCode);
        }

        var platform = factory.CreateClientFor(await factory.AddUserAsync(UserRole.PlatformAdmin, null));
        Assert.Equal(HttpStatusCode.Forbidden, (await platform.GetAsync($"{BillingUrl}/plans")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await platform.PostJsonAsync($"{BillingUrl}/checkout", body)).StatusCode);
    }

    [Fact]
    public async Task TheCatalog_ListsTheActivePaidPlansOfTheAccountKind()
    {
        var company = await AdminOfAsync(await factory.AddAgencyAsync());
        var forCompany = await CreatePlanAsync();
        var forAnyone = await CreatePlanAsync(PlanKind.Featured, PlanAudience.Any);
        var forPeople = await CreatePlanAsync(audience: PlanAudience.Individual);
        var inactive = await CreatePlanAsync(active: false);

        var plans = await (await company.GetAsync($"{BillingUrl}/plans")).ReadAsync<List<PlanDto>>();

        Assert.Contains(plans, p => p.Id == forCompany.Id);
        Assert.Contains(plans, p => p.Id == forAnyone.Id);
        Assert.DoesNotContain(plans, p => p.Id == forPeople.Id);
        Assert.DoesNotContain(plans, p => p.Id == inactive.Id);
        Assert.DoesNotContain(plans, p => p.IsFree);
    }

    [Fact]
    public async Task ABuy_CreatesTheSubscription_AndItShowsInTheAccountPlan()
    {
        var agency = await factory.AddAgencyAsync();
        var client = await AdminOfAsync(agency);
        var plan = await CreatePlanAsync();

        var response = await client.PostJsonAsync($"{BillingUrl}/checkout", new CheckoutRequest(plan.Id, Card()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var subscription = await response.ReadAsync<SubscriptionDto>();
        Assert.Equal(agency.Id, subscription.AgencyId);
        Assert.Equal(1399m, subscription.AmountPaid);
        Assert.StartsWith("SIM-", subscription.PaymentReference, StringComparison.Ordinal);
        Assert.True(subscription.IsActive);

        var mine = await (await client.GetAsync($"{BillingUrl}/me")).ReadAsync<AccountBillingDto>();
        Assert.Equal(plan.Name, mine.Listings.PlanName);
        Assert.Equal(200, mine.Listings.Limit);
        Assert.NotNull(mine.Listings.EndsAt);
    }

    [Fact]
    public async Task AFreeIndividualAccount_CanBuyAPlanForIndividuals()
    {
        var agency = Agency.CreateIndividual("Particular", $"{Guid.NewGuid():N}@individual.test");
        await factory.WithDbAsync(async db =>
        {
            db.Agencies.Add(agency);
            await db.SaveChangesAsync();
        });
        var client = await AdminOfAsync(agency);
        var plan = await CreatePlanAsync(audience: PlanAudience.Individual);

        var response = await client.PostJsonAsync($"{BillingUrl}/checkout", new CheckoutRequest(plan.Id, Card()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var mine = await (await client.GetAsync($"{BillingUrl}/me")).ReadAsync<AccountBillingDto>();
        Assert.False(mine.Listings.IsFree);
        Assert.Equal(plan.Name, mine.Listings.PlanName);
    }

    [Theory]
    [InlineData("4000 0000 0000 0002", "rechaz")]
    [InlineData("4000 0000 0000 9995", "fondos")]
    [InlineData("4000 0000 0000 0069", "vencida")]
    public async Task ADeclinedCard_Responds402_WithTheReason_AndChangesNothing(string number, string reason)
    {
        var agency = await factory.AddAgencyAsync();
        var client = await AdminOfAsync(agency);
        var plan = await CreatePlanAsync();

        var response = await client.PostJsonAsync($"{BillingUrl}/checkout", new CheckoutRequest(plan.Id, Card(number)));

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Contains(reason, (await response.ReadProblemAsync()).Detail, StringComparison.Ordinal);
        var mine = await (await client.GetAsync($"{BillingUrl}/me")).ReadAsync<AccountBillingDto>();
        Assert.Null(mine.Listings.PlanName);
    }

    [Fact]
    public async Task AnExpiredCard_Responds402()
    {
        var client = await AdminOfAsync(await factory.AddAgencyAsync());
        var plan = await CreatePlanAsync();

        var response = await client.PostJsonAsync($"{BillingUrl}/checkout", new CheckoutRequest(plan.Id, Card(month: 1, year: 2020)));

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
    }

    [Fact]
    public async Task InvalidCardData_Responds400_WithoutEchoingTheCard()
    {
        var client = await AdminOfAsync(await factory.AddAgencyAsync());
        var plan = await CreatePlanAsync();

        var response = await client.PostJsonAsync($"{BillingUrl}/checkout", new CheckoutRequest(plan.Id, Card("4242 4242 4242 4241", cvv: "12")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("4242", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APlanForAnotherAccountKind_AnInactiveOne_AndAnUnknownOne_AreRejected()
    {
        var client = await AdminOfAsync(await factory.AddAgencyAsync());
        var forPeople = await CreatePlanAsync(audience: PlanAudience.Individual);
        var inactive = await CreatePlanAsync(active: false);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostJsonAsync($"{BillingUrl}/checkout", new CheckoutRequest(forPeople.Id, Card()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostJsonAsync($"{BillingUrl}/checkout", new CheckoutRequest(inactive.Id, Card()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostJsonAsync($"{BillingUrl}/checkout", new CheckoutRequest(Guid.CreateVersion7(), Card()))).StatusCode);
    }

    [Fact]
    public async Task TheClientCannotChooseThePrice()
    {
        var agency = await factory.AddAgencyAsync();
        var client = await AdminOfAsync(agency);
        var plan = await CreatePlanAsync();

        // Un cuerpo con un monto inventado: el campo no existe en el contrato y se ignora; se cobra el precio del plan.
        var response = await client.PostAsync(
            $"{BillingUrl}/checkout",
            new StringContent(
                $$$"""{"planId":"{{{plan.Id}}}","amountPaid":1,"card":{"number":"4242424242424242","holderName":"ANA","expiryMonth":12,"expiryYear":2099,"cvv":"123"}}""",
                System.Text.Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1399m, (await response.ReadAsync<SubscriptionDto>()).AmountPaid);
    }
}
