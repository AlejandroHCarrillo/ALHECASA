using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Billing;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.Infrastructure.Payments;
using Alhecasa.UnitTests.Domain;
using Alhecasa.UnitTests.Support;

namespace Alhecasa.UnitTests.Application;

/// <summary>Compra de planes por la propia cuenta con la pasarela de pagos.</summary>
public sealed class CheckoutServiceTests
{
    private const string GoodCard = "4242 4242 4242 4242";

    private readonly FakePlanRepository _plans = new();
    private readonly FakeSubscriptionRepository _subscriptions = new();
    private readonly FakeAgencyRepository _agencies = new();
    private readonly FakePropertyRepository _properties = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakePaymentGateway _gateway = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly AccountLimits _limits = new(3);

    private BillingService CreateService(ICurrentUser user) => new(
        _subscriptions,
        _plans,
        _agencies,
        _properties,
        new EntitlementService(_subscriptions, _plans, _properties, _clock, _limits),
        _unitOfWork,
        user,
        new AssignPlanRequestValidator(),
        new CheckoutRequestValidator(),
        _gateway,
        _clock,
        _limits);

    private BillingService ServiceFor(Agency agency) => CreateService(FakeCurrentUser.AgencyAdmin(agency.Id));

    private Plan Listings(int? limit = 100, string name = "Inmobiliaria 100") =>
        _plans.Add(PlanTests.Paid(limit: limit) with { Name = name });

    private Plan Featured(int? limit = 3, string name = "3 destacados") =>
        _plans.Add(PlanTests.Paid(PlanKind.Featured, limit, BillingPeriod.Week, 199m) with { Name = name, Audience = PlanAudience.Any });

    private static PaymentCardRequest Card(string number = GoodCard, int month = 12, int year = 2030, string cvv = "123", string holder = "ANA LOPEZ") =>
        new(number, holder, month, year, cvv);

    // --- Quién compra ---------------------------------------------------------------------

    [Fact]
    public async Task OnlyTheAccountAdministrator_CanBuy()
    {
        var agency = _agencies.Add();
        var plan = Listings();
        var request = new CheckoutRequest(plan.Id, Card());

        foreach (var user in new ICurrentUser[]
                 {
                     FakeCurrentUser.PlatformAdmin(),
                     FakeCurrentUser.Client(),
                     new FakeCurrentUser { UserId = Guid.CreateVersion7(), Role = UserRole.Seller, AgencyId = agency.Id },
                     new FakeCurrentUser { UserId = Guid.CreateVersion7(), Role = UserRole.Supervisor, AgencyId = agency.Id }
                 })
        {
            var service = CreateService(user);
            await Assert.ThrowsAsync<ForbiddenException>(() => service.CheckoutAsync(request));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.ListAvailablePlansAsync());
        }

        Assert.Empty(_gateway.Charges);
        Assert.Empty(_subscriptions.Items);
    }

    // --- Catálogo -------------------------------------------------------------------------

    [Fact]
    public async Task TheCatalog_OffersOnlyActivePaidPlansForTheKindOfAccount()
    {
        var company = _agencies.Add();
        var person = _agencies.AddIndividual();
        _plans.Add(PlanTests.Free());
        Listings(name: "Para inmobiliarias");
        Featured(name: "Para cualquiera");
        _plans.Add(PlanTests.Paid() with { Name = "Para particulares", Audience = PlanAudience.Individual, Limit = 10 });
        _plans.Add(PlanTests.Paid() with { Name = "Retirado", IsActive = false });

        var forCompany = await ServiceFor(company).ListAvailablePlansAsync();
        var forPerson = await ServiceFor(person).ListAvailablePlansAsync();

        Assert.Equal(["Para cualquiera", "Para inmobiliarias"], forCompany.Select(p => p.Name).Order());
        Assert.Equal(["Para cualquiera", "Para particulares"], forPerson.Select(p => p.Name).Order());
        Assert.DoesNotContain(forCompany.Concat(forPerson), p => p.IsFree);
    }

    // --- Comprar --------------------------------------------------------------------------

    [Fact]
    public async Task ABuy_ChargesThePlanPrice_AndCreatesTheSubscription()
    {
        var agency = _agencies.Add();
        var plan = Listings();

        var dto = await ServiceFor(agency).CheckoutAsync(new CheckoutRequest(plan.Id, Card()));

        var charge = Assert.Single(_gateway.Charges);
        Assert.Equal(799m, charge.Amount);
        Assert.Equal(CurrencyCode.MXN, charge.Currency);
        Assert.Equal("4242424242424242", charge.Card.Number); // la pasarela recibe solo los dígitos
        Assert.Equal(799m, dto.AmountPaid);
        Assert.Equal("TX-1", dto.PaymentReference);
        Assert.Equal(_clock.Now, dto.StartsAt);
        Assert.Equal(_clock.Now.AddMonths(1), dto.EndsAt);
        Assert.True(dto.IsActive);
        Assert.Equal(agency.Id, Assert.Single(_subscriptions.Items).AgencyId);
    }

    [Fact]
    public async Task ADeclinedCharge_CreatesNothing_AndShowsTheReason()
    {
        var agency = _agencies.Add();
        var plan = Listings();
        _gateway.DeclineWith = "La tarjeta no tiene fondos suficientes.";

        var ex = await Assert.ThrowsAsync<PaymentDeclinedException>(() => ServiceFor(agency).CheckoutAsync(new CheckoutRequest(plan.Id, Card())));

        Assert.Equal("La tarjeta no tiene fondos suficientes.", ex.Message);
        Assert.Empty(_subscriptions.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ADeclinedCharge_KeepsTheCurrentPlan()
    {
        var agency = _agencies.Add();
        var current = Listings(name: "Actual");
        var bigger = Listings(500, "Mayor");
        await ServiceFor(agency).CheckoutAsync(new CheckoutRequest(current.Id, Card()));
        _gateway.DeclineWith = "Rechazada.";

        await Assert.ThrowsAsync<PaymentDeclinedException>(() => ServiceFor(agency).CheckoutAsync(new CheckoutRequest(bigger.Id, Card())));

        var only = Assert.Single(_subscriptions.Items);
        Assert.Equal("Actual", only.PlanName);
        Assert.True(only.IsActiveAt(_clock.Now));
    }

    [Fact]
    public async Task ANewListingsPlan_ReplacesTheCurrentOne()
    {
        var agency = _agencies.Add();
        var first = Listings(100, "Cien");
        var second = Listings(200, "Doscientos");
        await ServiceFor(agency).CheckoutAsync(new CheckoutRequest(first.Id, Card()));

        await ServiceFor(agency).CheckoutAsync(new CheckoutRequest(second.Id, Card()));

        Assert.Equal(2, _subscriptions.Items.Count);
        Assert.Equal(["Doscientos"], _subscriptions.Items.Where(s => s.IsActiveAt(_clock.Now)).Select(s => s.PlanName));
    }

    [Fact]
    public async Task ABuyOfAFeaturedPackage_ClearsTheFeaturedAds_AndKeepsTheListingsPlan()
    {
        var agency = _agencies.Add();
        var listings = Listings();
        var package = Featured();
        await ServiceFor(agency).CheckoutAsync(new CheckoutRequest(listings.Id, Card()));
        await ServiceFor(agency).CheckoutAsync(new CheckoutRequest(package.Id, Card()));
        var property = new Property(agency.Id, PropertyTests.ValidData() with { Status = PropertyStatus.Published });
        property.Feature(_clock.Now.AddDays(3), _clock.Now);
        _properties.Items.Add(property);

        await ServiceFor(agency).CheckoutAsync(new CheckoutRequest(package.Id, Card()));

        Assert.False(property.IsFeaturedAt(_clock.Now));
        Assert.Equal(2, _subscriptions.Items.Count(s => s.IsActiveAt(_clock.Now)));
    }

    [Fact]
    public async Task APlanForAnotherKindOfAccount_AnInactiveOne_TheFreeOne_AndAnUnknownOne_AreRejected_WithoutCharging()
    {
        var company = _agencies.Add();
        var forPeople = _plans.Add(PlanTests.Paid() with { Name = "Particulares", Audience = PlanAudience.Individual });
        var inactive = _plans.Add(PlanTests.Paid() with { Name = "Retirado", IsActive = false });
        var free = _plans.Add(PlanTests.Free());
        var service = ServiceFor(company);

        await Assert.ThrowsAsync<DomainException>(() => service.CheckoutAsync(new CheckoutRequest(forPeople.Id, Card())));
        await Assert.ThrowsAsync<DomainException>(() => service.CheckoutAsync(new CheckoutRequest(inactive.Id, Card())));
        await Assert.ThrowsAsync<DomainException>(() => service.CheckoutAsync(new CheckoutRequest(free.Id, Card())));
        await Assert.ThrowsAsync<NotFoundException>(() => service.CheckoutAsync(new CheckoutRequest(Guid.CreateVersion7(), Card())));

        Assert.Empty(_gateway.Charges);
    }

    [Fact]
    public async Task AnExpiredCard_IsDeclinedBeforeCharging_AndTheCurrentMonthStillWorks()
    {
        var agency = _agencies.Add();
        var plan = Listings();
        var service = ServiceFor(agency);

        var ex = await Assert.ThrowsAsync<PaymentDeclinedException>(() => service.CheckoutAsync(new CheckoutRequest(plan.Id, Card(month: 5, year: 2026))));
        Assert.Contains("vencida", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_gateway.Charges);

        // Una tarjeta vence al terminar su mes: en junio de 2026 la que dice 06/2026 todavía sirve.
        await service.CheckoutAsync(new CheckoutRequest(plan.Id, Card(month: 6, year: 2026)));
        Assert.Single(_gateway.Charges);
    }

    [Fact]
    public async Task InvalidCardData_IsRejectedBeforeCharging()
    {
        var agency = _agencies.Add();
        var plan = Listings();
        var service = ServiceFor(agency);

        foreach (var card in new[]
                 {
                     Card(number: "4242 4242 4242 4241"), // dígito de control
                     Card(number: "abcd"),
                     Card(number: "4242"),
                     Card(month: 13),
                     Card(month: 0),
                     Card(year: 1999),
                     Card(cvv: "12"),
                     Card(cvv: "12a"),
                     Card(holder: " ")
                 })
        {
            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => service.CheckoutAsync(new CheckoutRequest(plan.Id, card)));
        }

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => service.CheckoutAsync(new CheckoutRequest(plan.Id, null!)));
        Assert.Empty(_gateway.Charges);
    }

    [Fact]
    public async Task ValidationMessages_NeverRepeatTheCardNumber()
    {
        var agency = _agencies.Add();
        var plan = Listings();

        var ex = await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () => ServiceFor(agency).CheckoutAsync(new CheckoutRequest(plan.Id, Card(number: "4242 4242 4242 4241", cvv: "999x"))));

        Assert.DoesNotContain(ex.Errors, e => e.ErrorMessage.Contains("4242", StringComparison.Ordinal) || e.ErrorMessage.Contains("999", StringComparison.Ordinal));
    }

    [Fact]
    public void TheCardData_IsHiddenWhenPrinted()
    {
        var card = Card();
        var request = new PaymentRequest(799m, CurrencyCode.MXN, "Plan", new PaymentCard("4242424242424242", "ANA", 12, 2030, "123"));

        Assert.DoesNotContain("4242", card.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("4242", new CheckoutRequest(Guid.CreateVersion7(), card).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("4242", request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("4242", request.Card.ToString(), StringComparison.Ordinal);
    }
}

public sealed class SimulatedPaymentGatewayTests
{
    private static PaymentRequest Request(string number) =>
        new(799m, CurrencyCode.MXN, "Plan", new PaymentCard(number, "ANA LOPEZ", 12, 2030, "123"));

    [Theory]
    [InlineData("4242424242424242")]
    [InlineData("4242 4242 4242 4242")]
    [InlineData("5555555555554444")]
    public async Task AnyOtherValidCard_IsApproved_WithATransactionId(string number)
    {
        var result = await new SimulatedPaymentGateway().ChargeAsync(Request(number));

        Assert.True(result.Approved);
        Assert.StartsWith("SIM-", result.TransactionId, StringComparison.Ordinal);
        Assert.Null(result.DeclineReason);
    }

    [Theory]
    [InlineData("4000000000000002", "rechazó")]
    [InlineData("4000 0000 0000 9995", "fondos")]
    [InlineData("4000000000000069", "vencida")]
    public async Task TheTestCards_AreDeclined_WithAReasonToShow(string number, string reason)
    {
        var result = await new SimulatedPaymentGateway().ChargeAsync(Request(number));

        Assert.False(result.Approved);
        Assert.Null(result.TransactionId);
        Assert.Contains(reason, result.DeclineReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EachApprovedCharge_GetsItsOwnTransactionId()
    {
        var gateway = new SimulatedPaymentGateway();

        var first = await gateway.ChargeAsync(Request("4242424242424242"));
        var second = await gateway.ChargeAsync(Request("4242424242424242"));

        Assert.NotEqual(first.TransactionId, second.TransactionId);
    }

    [Theory]
    [InlineData("4242 4242 4242 4242", true)]
    [InlineData("4000-0000-0000-0002", true)]
    [InlineData("4242 4242 4242 4241", false)]
    [InlineData("4242", false)]
    [InlineData("4242 4242 4242 424a", false)]
    [InlineData("", false)]
    public void CardNumbers_AreCheckedWithLuhn(string number, bool valid)
    {
        var validator = new PaymentCardRequestValidator();

        var result = validator.Validate(new PaymentCardRequest(number, "ANA", 12, 2030, "123"));

        Assert.Equal(valid, !result.Errors.Any(e => e.PropertyName == nameof(PaymentCardRequest.Number)));
    }
}
