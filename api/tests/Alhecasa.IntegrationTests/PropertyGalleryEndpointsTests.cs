using System.Net;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;

namespace Alhecasa.IntegrationTests;

/// <summary>Galería de la inmobiliaria: sus propiedades publicadas tal como las ve un visitante.</summary>
public sealed class PropertyGalleryEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/properties/gallery";

    [Theory]
    [InlineData(UserRole.AgencyAdmin)]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Seller)]
    public async Task AgencyStaff_SeeOnlyThePublishedPropertiesOfTheirAgency_WithPublicDataOnly(UserRole role)
    {
        var agency = await factory.AddAgencyAsync("Mi inmobiliaria");
        var other = await factory.AddAgencyAsync("Otra");
        var mine = await factory.AddPropertyAsync(agency.Id, "Publicada mía", status: PropertyStatus.Published, street: "Calle Secreta 123", latitude: 19.4326077m, longitude: -99.1332079m);
        await factory.AddPropertyAsync(agency.Id, "Borrador mío", status: PropertyStatus.Draft);
        await factory.AddPropertyAsync(agency.Id, "Pausada mía", status: PropertyStatus.Paused);
        await factory.AddPropertyAsync(other.Id, "Publicada ajena", status: PropertyStatus.Published);
        var staff = await factory.AddUserAsync(role, agency.Id);

        var response = await factory.CreateClientFor(staff).GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Secreta", body, StringComparison.Ordinal);
        Assert.DoesNotContain("19.4326", body, StringComparison.Ordinal);
        var page = System.Text.Json.JsonSerializer.Deserialize<PagedResult<PublicPropertyDto>>(body, HttpExtensions.JsonOptions)!;
        var item = Assert.Single(page.Items);
        Assert.Equal(mine.Id, item.Id);
        Assert.Equal("Mi inmobiliaria", item.AgencyName);
        Assert.Equal(19.433m, item.ApproximateLatitude);
    }

    [Fact]
    public async Task ItShowsTheSameAsThePublicSite_ForTheSameAgency()
    {
        var agency = await factory.AddAgencyAsync();
        var staff = await factory.AddUserAsync(UserRole.Seller, agency.Id);
        var property = await factory.AddPropertyAsync(agency.Id, "En ambos lados", status: PropertyStatus.Published);

        var gallery = await (await factory.CreateClientFor(staff).GetAsync(Url)).ReadAsync<PagedResult<PublicPropertyDto>>();
        var publicPage = await (await factory.CreateClient().GetAsync($"/api/public/properties?ids={property.Id}")).ReadAsync<PagedResult<PublicPropertyDto>>();

        Assert.Equivalent(publicPage.Items, gallery.Items);
    }

    [Fact]
    public async Task ItPaginates_NewestFirst()
    {
        var agency = await factory.AddAgencyAsync();
        var staff = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        for (var i = 0; i < 3; i++)
        {
            await factory.AddPropertyAsync(agency.Id, $"Galería {i}", status: PropertyStatus.Published);
        }

        var response = await factory.CreateClientFor(staff).GetAsync($"{Url}?page=2&pageSize=2");
        var page = await response.ReadAsync<PagedResult<PublicPropertyDto>>();

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal("Galería 0", Assert.Single(page.Items).Title);
    }

    [Fact]
    public async Task APropertyOfAnInactiveAgency_IsNotShown_BecauseTheSiteDoesNotShowIt()
    {
        var agency = await factory.AddAgencyAsync();
        var staff = await factory.AddUserAsync(UserRole.Seller, agency.Id);
        await factory.AddPropertyAsync(agency.Id, "Invisible", status: PropertyStatus.Published);
        await factory.ModifyAgencyAsync(agency.Id, a => a.MarkAsDeleted(DateTimeOffset.UtcNow));

        var response = await factory.CreateClientFor(staff).GetAsync(Url);

        // La sesión puede seguir válida un instante; si no, se rechaza. Lo que no puede pasar es que se muestre.
        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Empty((await response.ReadAsync<PagedResult<PublicPropertyDto>>()).Items);
        }
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
}