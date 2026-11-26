using System.Net;
using Alhecasa.Application.Dashboard;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.IntegrationTests;

/// <summary>Resumen de la página de inicio de la inmobiliaria: solo su personal, solo de su inmobiliaria.</summary>
public sealed class DashboardEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/dashboard";

    private async Task<AgencyDashboardDto> GetAsync(User user)
    {
        var response = await factory.CreateClientFor(user).GetAsync(Url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<AgencyDashboardDto>();
    }

    [Fact]
    public async Task WithoutToken_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(Url)).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.PlatformAdmin)]
    [InlineData(UserRole.Client)]
    public async Task PlatformAdminAndClients_Return403(UserRole role)
    {
        var agency = await factory.AddAgencyAsync();
        var user = await factory.AddUserAsync(role, role == UserRole.PlatformAdmin ? null : agency.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await factory.CreateClientFor(user).GetAsync(Url)).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.AgencyAdmin)]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Seller)]
    public async Task AgencyStaff_GetAnEmptySummaryForANewAgency(UserRole role)
    {
        var agency = await factory.AddAgencyAsync();
        var user = await factory.AddUserAsync(role, agency.Id);

        var dashboard = await GetAsync(user);

        Assert.Equal(new PropertyStats(0, 0, 0, 0, 0, 0), dashboard.Properties);
        Assert.Equal(0, dashboard.Inquiries.Total);
        Assert.Empty(dashboard.LatestNewInquiries);
    }

    [Fact]
    public async Task CountsPropertiesByStatus_IncludingFeaturedAndExcludingDeleted()
    {
        var agency = await factory.AddAgencyAsync();
        var admin = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);
        var featured = await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);
        await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Draft);
        await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Paused);
        await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Closed);
        var deleted = await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);

        await factory.WithDbAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;
            (await db.Properties.IgnoreQueryFilters().SingleAsync(p => p.Id == featured.Id)).Feature(now.AddDays(5), now);
            (await db.Properties.IgnoreQueryFilters().SingleAsync(p => p.Id == deleted.Id)).MarkAsDeleted(now);
            await db.SaveChangesAsync();
        });

        var stats = (await GetAsync(admin)).Properties;

        Assert.Equal(new PropertyStats(Total: 5, Published: 2, Draft: 1, Paused: 1, Closed: 1, Featured: 1), stats);
    }

    [Fact]
    public async Task CountsInquiriesByStage_AndListsTheNewestNewOnes()
    {
        var agency = await factory.AddAgencyAsync();
        var seller = await factory.AddUserAsync(UserRole.Seller, agency.Id);
        var property = await factory.AddPropertyAsync(agency.Id, "Casa en Polanco", status: PropertyStatus.Published);
        await factory.AddInquiryAsync(agency.Id, property.Id, "Primera", "Casa en Polanco");
        await factory.AddInquiryAsync(agency.Id, property.Id, "Segunda", "Casa en Polanco");
        var attended = await factory.AddInquiryAsync(agency.Id, property.Id, "Atendida", "Casa en Polanco");
        var closed = await factory.AddInquiryAsync(agency.Id, property.Id, "Cerrada", "Casa en Polanco");

        await factory.WithDbAsync(async db =>
        {
            (await db.Inquiries.IgnoreQueryFilters().SingleAsync(i => i.Id == attended.Id)).ChangeStatus(InquiryStatus.InProgress);
            (await db.Inquiries.IgnoreQueryFilters().SingleAsync(i => i.Id == closed.Id)).ChangeStatus(InquiryStatus.Closed);
            await db.SaveChangesAsync();
        });

        var dashboard = await GetAsync(seller);

        Assert.Equal(2, dashboard.Inquiries.New);
        Assert.Equal(1, dashboard.Inquiries.InProgress);
        Assert.Equal(1, dashboard.Inquiries.Closed);
        Assert.Equal(4, dashboard.Inquiries.ReceivedRecently);
        Assert.Equal(["Segunda", "Primera"], dashboard.LatestNewInquiries.Select(i => i.Name));
        Assert.All(dashboard.LatestNewInquiries, i => Assert.Equal("Casa en Polanco", i.PropertyTitle));
    }

    [Fact]
    public async Task NeverMixesInAnotherAgenciesNumbers()
    {
        var mine = await factory.AddAgencyAsync("Mía");
        var other = await factory.AddAgencyAsync("Otra");
        var admin = await factory.AddUserAsync(UserRole.AgencyAdmin, mine.Id);
        var myProperty = await factory.AddPropertyAsync(mine.Id, status: PropertyStatus.Published);
        var otherProperty = await factory.AddPropertyAsync(other.Id, status: PropertyStatus.Published);
        await factory.AddPropertyAsync(other.Id, status: PropertyStatus.Draft);
        await factory.AddInquiryAsync(mine.Id, myProperty.Id, "Mía");
        await factory.AddInquiryAsync(other.Id, otherProperty.Id, "Ajena");

        var dashboard = await GetAsync(admin);

        Assert.Equal(1, dashboard.Properties.Total);
        Assert.Equal(1, dashboard.Inquiries.Total);
        Assert.Equal(["Mía"], dashboard.LatestNewInquiries.Select(i => i.Name));
    }
}