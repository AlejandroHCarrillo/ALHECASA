using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Application.Dashboard;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.UnitTests.Domain;
using Alhecasa.UnitTests.Support;

namespace Alhecasa.UnitTests.Application;

/// <summary>Resumen de la página de inicio: propiedades por estado, solicitudes por etapa y las nuevas más recientes.</summary>
public sealed class DashboardServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly FakePropertyRepository _properties = new();
    private readonly FakeInquiryRepository _inquiries = new();
    private readonly Guid _agencyId = Guid.CreateVersion7();

    private DashboardService CreateService(ICurrentUser user) => new(_properties, _inquiries, user, new FakeClock(Now));

    private DashboardService AdminService() => CreateService(FakeCurrentUser.AgencyAdmin(_agencyId));

    private Property AddProperty(PropertyStatus status, Guid? agencyId = null, bool featured = false, bool deleted = false)
    {
        var property = new Property(agencyId ?? _agencyId, PropertyTests.ValidData() with { Status = status });
        if (featured)
        {
            property.Feature(Now.AddDays(3), Now);
        }

        if (deleted)
        {
            property.MarkAsDeleted(Now);
        }

        _properties.Items.Add(property);
        return property;
    }

    private Inquiry AddInquiry(InquiryStatus status = InquiryStatus.New, Guid? agencyId = null, DateTimeOffset? createdAt = null, string name = "Ana López")
    {
        var inquiry = new Inquiry(Guid.CreateVersion7(), agencyId ?? _agencyId, "Casa en Polanco", name, "5512345678", "ana@example.com", "¿Sigue disponible?")
        {
            CreatedAt = createdAt ?? Now.AddDays(-1)
        };
        if (status != InquiryStatus.New)
        {
            inquiry.ChangeStatus(status);
        }

        _inquiries.Items.Add(inquiry);
        return inquiry;
    }

    [Fact]
    public async Task EmptyAccount_ReportsZeros()
    {
        var dashboard = await AdminService().GetAsync();

        Assert.Equal(new PropertyStats(0, 0, 0, 0, 0, 0), dashboard.Properties);
        Assert.Equal(new InquiryStats(0, 0, 0, 0), dashboard.Inquiries);
        Assert.Empty(dashboard.LatestNewInquiries);
        Assert.Equal(DashboardService.RecentDays, dashboard.RecentDays);
    }

    [Fact]
    public async Task Properties_AreCountedByStatus_AndFeaturedOnes()
    {
        AddProperty(PropertyStatus.Published);
        AddProperty(PropertyStatus.Published, featured: true);
        AddProperty(PropertyStatus.Draft);
        AddProperty(PropertyStatus.Paused);
        AddProperty(PropertyStatus.Closed);
        AddProperty(PropertyStatus.Closed);

        var stats = (await AdminService().GetAsync()).Properties;

        Assert.Equal(new PropertyStats(Total: 6, Published: 2, Draft: 1, Paused: 1, Closed: 2, Featured: 1), stats);
    }

    [Fact]
    public async Task ExpiredFeatured_IsNotCounted()
    {
        // Se destacó hace dos horas y el destacado venció hace una.
        var property = AddProperty(PropertyStatus.Published);
        property.Feature(Now.AddHours(-1), Now.AddHours(-2));

        Assert.Equal(0, (await AdminService().GetAsync()).Properties.Featured);
    }

    [Fact]
    public async Task Properties_IgnoreDeletedOnesAndOtherAgencies()
    {
        AddProperty(PropertyStatus.Published);
        AddProperty(PropertyStatus.Published, deleted: true);
        AddProperty(PropertyStatus.Published, agencyId: Guid.CreateVersion7());

        var stats = (await AdminService().GetAsync()).Properties;

        Assert.Equal(1, stats.Total);
        Assert.Equal(1, stats.Published);
    }

    [Fact]
    public async Task Inquiries_AreCountedByStage_AndRecentOnesInTheWindow()
    {
        AddInquiry(InquiryStatus.New, createdAt: Now.AddDays(-1));
        AddInquiry(InquiryStatus.New, createdAt: Now.AddDays(-30));
        AddInquiry(InquiryStatus.InProgress, createdAt: Now.AddDays(-2));
        AddInquiry(InquiryStatus.Closed, createdAt: Now.AddDays(-60));
        AddInquiry(InquiryStatus.New, agencyId: Guid.CreateVersion7());

        var stats = (await AdminService().GetAsync()).Inquiries;

        Assert.Equal(2, stats.New);
        Assert.Equal(1, stats.InProgress);
        Assert.Equal(1, stats.Closed);
        Assert.Equal(4, stats.Total);
        Assert.Equal(2, stats.ReceivedRecently);
    }

    [Fact]
    public async Task LatestNewInquiries_AreOnlyNewOnes_NewestFirst_AtMostFive()
    {
        for (var i = 0; i < 7; i++)
        {
            AddInquiry(createdAt: Now.AddHours(-i - 1), name: $"Prospecto {i}");
        }

        AddInquiry(InquiryStatus.InProgress, createdAt: Now);
        AddInquiry(InquiryStatus.Closed, createdAt: Now);

        var latest = (await AdminService().GetAsync()).LatestNewInquiries;

        Assert.Equal(DashboardService.LatestInquiriesCount, latest.Count);
        Assert.Equal(["Prospecto 0", "Prospecto 1", "Prospecto 2", "Prospecto 3", "Prospecto 4"], latest.Select(i => i.Name));
    }

    [Theory]
    [InlineData(UserRole.AgencyAdmin)]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Seller)]
    public async Task AgencyStaff_CanSeeIt(UserRole role)
    {
        AddProperty(PropertyStatus.Published);
        var user = new FakeCurrentUser { UserId = Guid.CreateVersion7(), Role = role, AgencyId = _agencyId };

        var dashboard = await CreateService(user).GetAsync();

        Assert.Equal(1, dashboard.Properties.Published);
    }

    [Fact]
    public async Task PlatformAdmin_IsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService(FakeCurrentUser.PlatformAdmin()).GetAsync());
    }

    [Fact]
    public async Task Client_IsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService(FakeCurrentUser.Client()).GetAsync());
    }

    [Fact]
    public async Task StaffWithoutAnAgency_IsForbidden()
    {
        var user = new FakeCurrentUser { UserId = Guid.CreateVersion7(), Role = UserRole.AgencyAdmin };

        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService(user).GetAsync());
    }
}