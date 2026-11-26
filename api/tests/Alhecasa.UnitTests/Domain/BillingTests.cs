using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;

namespace Alhecasa.UnitTests.Domain;

public sealed class PlanTests
{
    internal static PlanData Paid(PlanKind kind = PlanKind.Listings, int? limit = 100, BillingPeriod? period = BillingPeriod.Month, decimal price = 799m) =>
        new("Inmobiliaria 100", "Hasta 100 publicaciones", kind, PlanAudience.Company, limit, period, price, CurrencyCode.MXN);

    internal static PlanData Free(int? limit = 3) =>
        new("Gratuito", null, PlanKind.Listings, PlanAudience.Individual, limit, null, 0m, CurrencyCode.MXN);

    [Fact]
    public void APaidPlan_KeepsItsData_AndIsNotFree()
    {
        var plan = new Plan(Paid());

        Assert.Equal("Inmobiliaria 100", plan.Name);
        Assert.Equal(100, plan.Limit);
        Assert.Equal(BillingPeriod.Month, plan.Period);
        Assert.False(plan.IsFree);
        Assert.True(plan.IsActive);
    }

    [Fact]
    public void ThePlanName_IsTrimmed_AndRequired()
    {
        Assert.Equal("Plan", new Plan(Paid() with { Name = "  Plan  " }).Name);
        Assert.Throws<DomainException>(() => new Plan(Paid() with { Name = "   " }));
    }

    [Fact]
    public void ALimitOfNull_MeansUnlimited()
    {
        Assert.Null(new Plan(Paid(limit: null)).Limit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(Plan.MaxLimit + 1)]
    public void ALimitOutOfRange_IsRejected(int limit)
    {
        Assert.Throws<DomainException>(() => new Plan(Paid(limit: limit)));
    }

    [Fact]
    public void APaidPlan_NeedsABillingPeriod()
    {
        Assert.Throws<DomainException>(() => new Plan(Paid(period: null)));
    }

    [Theory]
    [InlineData(BillingPeriod.Day)]
    [InlineData(BillingPeriod.Week)]
    [InlineData(BillingPeriod.Month)]
    [InlineData(BillingPeriod.Year)]
    public void EveryBillingPeriod_IsAccepted(BillingPeriod period)
    {
        Assert.Equal(period, new Plan(Paid(period: period)).Period);
    }

    [Fact]
    public void ANegativePrice_IsRejected()
    {
        Assert.Throws<DomainException>(() => new Plan(Paid(price: -1m)));
    }

    [Fact]
    public void TheFreePlan_HasNoPeriod_EvenIfOneIsSent()
    {
        var plan = new Plan(Free() with { Period = BillingPeriod.Year });

        Assert.True(plan.IsFree);
        Assert.Null(plan.Period);
        Assert.Equal(3, plan.Limit);
    }

    [Fact]
    public void TheFreePlan_MustBeListings_ForIndividuals_WithALimit_AndActive()
    {
        Assert.Throws<DomainException>(() => new Plan(Free() with { Kind = PlanKind.Featured }));
        Assert.Throws<DomainException>(() => new Plan(Free() with { Audience = PlanAudience.Company }));
        Assert.Throws<DomainException>(() => new Plan(Free() with { Audience = PlanAudience.Any }));
        Assert.Throws<DomainException>(() => new Plan(Free(limit: null)));
        Assert.Throws<DomainException>(() => new Plan(Free() with { IsActive = false }));
    }

    [Fact]
    public void AnUnknownKindAudienceOrCurrency_IsRejected()
    {
        Assert.Throws<DomainException>(() => new Plan(Paid() with { Kind = (PlanKind)99 }));
        Assert.Throws<DomainException>(() => new Plan(Paid() with { Audience = (PlanAudience)99 }));
        Assert.Throws<DomainException>(() => new Plan(Paid() with { Currency = (CurrencyCode)99 }));
        Assert.Throws<DomainException>(() => new Plan(Paid(period: (BillingPeriod)99)));
    }

    [Theory]
    [InlineData(PlanAudience.Individual, true, true)]
    [InlineData(PlanAudience.Individual, false, false)]
    [InlineData(PlanAudience.Company, true, false)]
    [InlineData(PlanAudience.Company, false, true)]
    [InlineData(PlanAudience.Any, true, true)]
    [InlineData(PlanAudience.Any, false, true)]
    public void AcceptsAccount_FollowsTheAudience(PlanAudience audience, bool isIndividual, bool expected)
    {
        var plan = new Plan(Paid() with { Audience = audience });

        Assert.Equal(expected, plan.AcceptsAccount(isIndividual));
    }
}

public sealed class SubscriptionTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 31, 10, 0, 0, TimeSpan.Zero);

    private static Subscription Subscribe(PlanData? plan = null, decimal amount = 799m, string? reference = null) =>
        new(Guid.CreateVersion7(), new Plan(plan ?? PlanTests.Paid()), Start, amount, reference);

    [Theory]
    [InlineData(BillingPeriod.Day, "2026-02-01T10:00:00+00:00")]
    [InlineData(BillingPeriod.Week, "2026-02-07T10:00:00+00:00")]
    [InlineData(BillingPeriod.Month, "2026-02-28T10:00:00+00:00")]
    [InlineData(BillingPeriod.Year, "2027-01-31T10:00:00+00:00")]
    public void ItLastsExactlyOnePeriod(BillingPeriod period, string expectedEnd)
    {
        var subscription = Subscribe(PlanTests.Paid(period: period));

        Assert.Equal(DateTimeOffset.Parse(expectedEnd, System.Globalization.CultureInfo.InvariantCulture), subscription.EndsAt);
        Assert.Equal(Start, subscription.StartsAt);
    }

    [Fact]
    public void ItCopiesWhatWasPurchased_SoEditingThePlanLaterChangesNothing()
    {
        var plan = new Plan(PlanTests.Paid(limit: 200));
        var subscription = new Subscription(Guid.CreateVersion7(), plan, Start, 1_399m, "  SPEI 123  ");

        plan.Update(PlanTests.Paid(limit: 500) with { Name = "Otro nombre" });

        Assert.Equal(200, subscription.Limit);
        Assert.Equal("Inmobiliaria 100", subscription.PlanName);
        Assert.Equal(1_399m, subscription.AmountPaid);
        Assert.Equal("SPEI 123", subscription.PaymentReference);
        Assert.Equal(plan.Id, subscription.PlanId);
    }

    [Fact]
    public void ABlankPaymentReference_IsStoredAsNull()
    {
        Assert.Null(Subscribe(reference: "   ").PaymentReference);
    }

    [Fact]
    public void AnUnlimitedPlan_YieldsAnUnlimitedSubscription()
    {
        Assert.Null(Subscribe(PlanTests.Paid(limit: null)).Limit);
    }

    [Fact]
    public void AFreePlan_CannotBeSubscribedTo()
    {
        Assert.Throws<DomainException>(() => new Subscription(Guid.CreateVersion7(), new Plan(PlanTests.Free()), Start, 0m, null));
    }

    [Fact]
    public void ItNeedsAnAccount_AndAValidAmount()
    {
        var plan = new Plan(PlanTests.Paid());

        Assert.Throws<DomainException>(() => new Subscription(Guid.Empty, plan, Start, 1m, null));
        Assert.Throws<DomainException>(() => new Subscription(Guid.CreateVersion7(), plan, Start, -1m, null));
    }

    [Fact]
    public void ItIsActiveFromItsStartUntilItsEnd_AndNotAfterBeingCancelled()
    {
        var subscription = Subscribe();

        Assert.False(subscription.IsActiveAt(Start.AddSeconds(-1)));
        Assert.True(subscription.IsActiveAt(Start));
        Assert.True(subscription.IsActiveAt(subscription.EndsAt.AddSeconds(-1)));
        Assert.False(subscription.IsActiveAt(subscription.EndsAt));

        subscription.Cancel(Start.AddDays(1));
        Assert.False(subscription.IsActiveAt(Start.AddDays(2)));
        Assert.Equal(Start.AddDays(1), subscription.CancelledAt);
    }

    [Fact]
    public void CancellingTwice_KeepsTheFirstDate()
    {
        var subscription = Subscribe();

        subscription.Cancel(Start.AddDays(1));
        subscription.Cancel(Start.AddDays(5));

        Assert.Equal(Start.AddDays(1), subscription.CancelledAt);
    }
}

public sealed class PropertyFeaturedTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static Property NewProperty() => new(Guid.CreateVersion7(), PropertyTests.ValidData());

    [Fact]
    public void ANewProperty_IsNotFeatured()
    {
        var property = NewProperty();

        Assert.Null(property.FeaturedUntil);
        Assert.False(property.IsFeaturedAt(Now));
    }

    [Fact]
    public void Feature_MakesItFeaturedUntilTheGivenDate_ThenItStopsOnItsOwn()
    {
        var property = NewProperty();

        property.Feature(Now.AddDays(7), Now);

        Assert.True(property.IsFeaturedAt(Now));
        Assert.True(property.IsFeaturedAt(Now.AddDays(7).AddSeconds(-1)));
        Assert.False(property.IsFeaturedAt(Now.AddDays(7)));
    }

    [Fact]
    public void Feature_RejectsAPastDate()
    {
        Assert.Throws<DomainException>(() => NewProperty().Feature(Now, Now));
        Assert.Throws<DomainException>(() => NewProperty().Feature(Now.AddDays(-1), Now));
    }

    [Fact]
    public void Unfeature_RemovesIt()
    {
        var property = NewProperty();
        property.Feature(Now.AddDays(7), Now);

        property.Unfeature();

        Assert.False(property.IsFeaturedAt(Now));
        Assert.Null(property.FeaturedUntil);
    }

    [Fact]
    public void EditingAProperty_DoesNotChangeItsFeaturedState()
    {
        var property = NewProperty();
        property.Feature(Now.AddDays(7), Now);

        property.Update(PropertyTests.ValidData() with { Title = "Otro título" });

        Assert.True(property.IsFeaturedAt(Now));
    }
}
