using System.Net;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.IntegrationTests;

/// <summary>Catálogo público (portada de visitantes): sin sesión, solo lo publicado, sin datos privados.</summary>
public sealed class PublicPropertiesEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/public/properties";

    private async Task<PagedResult<PublicPropertyDto>> ListAsync(string query = "")
    {
        var response = await factory.CreateClient().GetAsync($"{Url}?pageSize=100{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResult<PublicPropertyDto>>();
    }

    [Fact]
    public async Task WithoutToken_ListsPublishedPropertiesOfEveryAgency()
    {
        var a = await factory.AddAgencyAsync("Agencia Norte");
        var b = await factory.AddAgencyAsync("Agencia Sur");
        var fromA = await factory.AddPropertyAsync(a.Id, "Publicada de A", status: PropertyStatus.Published);
        var fromB = await factory.AddPropertyAsync(b.Id, "Publicada de B", status: PropertyStatus.Published);

        var page = await ListAsync();

        Assert.Contains(page.Items, p => p.Id == fromA.Id && p.AgencyName == "Agencia Norte");
        Assert.Contains(page.Items, p => p.Id == fromB.Id && p.AgencyName == "Agencia Sur");
    }

    [Theory]
    [InlineData(PropertyStatus.Draft)]
    [InlineData(PropertyStatus.Paused)]
    [InlineData(PropertyStatus.Closed)]
    public async Task OnlyPublishedPropertiesAreVisible(PropertyStatus status)
    {
        var agency = await factory.AddAgencyAsync();
        var hidden = await factory.AddPropertyAsync(agency.Id, $"Oculta {status}", status: status);

        var page = await ListAsync();

        Assert.DoesNotContain(page.Items, p => p.Id == hidden.Id);
    }

    [Fact]
    public async Task DeletedProperties_AreNotVisible()
    {
        var agency = await factory.AddAgencyAsync();
        var property = await factory.AddPropertyAsync(agency.Id, "Se borrará", status: PropertyStatus.Published);
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Properties.IgnoreQueryFilters().SingleAsync(p => p.Id == property.Id);
            stored.MarkAsDeleted(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });

        var page = await ListAsync();

        Assert.DoesNotContain(page.Items, p => p.Id == property.Id);
    }

    [Fact]
    public async Task PropertiesOfADeletedAgency_AreNotVisible()
    {
        var agency = await factory.AddAgencyAsync();
        var property = await factory.AddPropertyAsync(agency.Id, "De agencia dada de baja", status: PropertyStatus.Published);
        await factory.ModifyAgencyAsync(agency.Id, a => a.MarkAsDeleted(DateTimeOffset.UtcNow));

        var page = await ListAsync();

        Assert.DoesNotContain(page.Items, p => p.Id == property.Id);
    }

    [Fact]
    public async Task DoesNotExposeTheStreetOrThePostalCode()
    {
        var agency = await factory.AddAgencyAsync();
        await factory.AddPropertyAsync(agency.Id, "Con calle privada", status: PropertyStatus.Published, street: "Calle Secreta 123");

        var response = await factory.CreateClient().GetAsync($"{Url}?pageSize=100");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Con calle privada", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Secreta", body, StringComparison.Ordinal);
        Assert.DoesNotContain("street", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("postalCode", body, StringComparison.OrdinalIgnoreCase);
    }

    // --- Detalle ----------------------------------------------------------------------------

    [Fact]
    public async Task Detail_ReturnsThePublishedProperty_WithAllItsImages_AndNoPrivateData()
    {
        var agency = await factory.AddAgencyAsync("Agencia Detalle");
        var property = await factory.AddPropertyAsync(agency.Id, "Casa con detalle", status: PropertyStatus.Published, street: "Calle Secreta 123");
        var images = await factory.AddImagesAsync(property.Id, 4);

        var response = await factory.CreateClient().GetAsync($"{Url}/{property.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Secreta", body, StringComparison.Ordinal);
        Assert.DoesNotContain("postalCode", body, StringComparison.OrdinalIgnoreCase);
        var dto = System.Text.Json.JsonSerializer.Deserialize<PublicPropertyDto>(body, HttpExtensions.JsonOptions)!;
        Assert.Equal("Casa con detalle", dto.Title);
        Assert.Equal("Agencia Detalle", dto.AgencyName);
        Assert.Equal(images.Select(i => i.Id), dto.ImageIds);
    }

    [Theory]
    [InlineData(PropertyStatus.Draft)]
    [InlineData(PropertyStatus.Paused)]
    [InlineData(PropertyStatus.Closed)]
    public async Task Detail_OfAPropertyThatIsNotPublished_Returns404(PropertyStatus status)
    {
        var agency = await factory.AddAgencyAsync();
        var property = await factory.AddPropertyAsync(agency.Id, status: status);

        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync($"{Url}/{property.Id}")).StatusCode);
    }

    [Fact]
    public async Task Detail_OfDeletedPropertyOrInactiveAgency_OrUnknownId_Returns404()
    {
        var agency = await factory.AddAgencyAsync();
        var deleted = await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Properties.IgnoreQueryFilters().SingleAsync(p => p.Id == deleted.Id);
            stored.MarkAsDeleted(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });
        var otherAgency = await factory.AddAgencyAsync();
        var orphan = await factory.AddPropertyAsync(otherAgency.Id, status: PropertyStatus.Published);
        await factory.ModifyAgencyAsync(otherAgency.Id, a => a.MarkAsDeleted(DateTimeOffset.UtcNow));

        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Url}/{deleted.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Url}/{orphan.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Location_MatchesCity_AndNotTheTitleNorTheStreet_AndEscapesWildcards()
    {
        var agency = await factory.AddAgencyAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var byCity = await factory.AddPropertyAsync(agency.Id, "Casa uno", city: $"Ciudad{marker}", status: PropertyStatus.Published);
        await factory.AddPropertyAsync(agency.Id, $"Título {marker}", status: PropertyStatus.Published);
        await factory.AddPropertyAsync(agency.Id, "Casa con calle", status: PropertyStatus.Published, street: $"Calle {marker}");

        var page = await ListAsync($"&location={marker}");
        Assert.Equal([byCity.Id], page.Items.Select(p => p.Id));

        Assert.Empty((await ListAsync("&location=%25")).Items);
    }

    [Fact]
    public async Task Location_MatchesNeighborhoodAndState_IgnoringCase()
    {
        var agency = await factory.AddAgencyAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        await factory.WithDbAsync(async db =>
        {
            db.Properties.Add(new Domain.Entities.Property(agency.Id, new Domain.Entities.PropertyData(
                "Con colonia", null, PropertyType.House, OperationType.Sale, PropertyStatus.Published, 1m, CurrencyCode.MXN,
                null, $"Colonia{marker}", "Ciudad", $"Estado{marker}", null, null, null, null, null)));
            await db.SaveChangesAsync();
        });

        Assert.Single((await ListAsync($"&location=colonia{marker.ToUpperInvariant()}")).Items);
        Assert.Single((await ListAsync($"&location=ESTADO{marker}")).Items);
    }

    [Fact]
    public async Task FiltersByOperationAndType_AndCombinesThem()
    {
        var agency = await factory.AddAgencyAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var city = $"Ciudad{marker}";
        var houseForSale = await factory.AddPropertyAsync(agency.Id, "Casa en venta", city, PropertyStatus.Published, type: PropertyType.House, operation: OperationType.Sale);
        var houseForRent = await factory.AddPropertyAsync(agency.Id, "Casa en renta", city, PropertyStatus.Published, type: PropertyType.House, operation: OperationType.Rent);
        var flatForRent = await factory.AddPropertyAsync(agency.Id, "Depto en renta", city, PropertyStatus.Published, type: PropertyType.Apartment, operation: OperationType.Rent);

        var rent = await ListAsync($"&location={city}&operation=Rent");
        Assert.Equal(new[] { houseForRent.Id, flatForRent.Id }.Order(), rent.Items.Select(p => p.Id).Order());
        Assert.All(rent.Items, p => Assert.Equal(OperationType.Rent, p.Operation));

        var houses = await ListAsync($"&location={city}&type=House");
        Assert.Equal(new[] { houseForSale.Id, houseForRent.Id }.Order(), houses.Items.Select(p => p.Id).Order());

        var both = await ListAsync($"&location={city}&type=Apartment&operation=Rent");
        Assert.Equal([flatForRent.Id], both.Items.Select(p => p.Id));

        Assert.Empty((await ListAsync($"&location={city}&type=Land")).Items);
    }

    [Theory]
    [InlineData("operation=Lease")]
    [InlineData("type=Castle")]
    public async Task AnUnknownOperationOrType_Returns400(string query)
    {
        var response = await factory.CreateClient().GetAsync($"{Url}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Paginates()
    {
        var agency = await factory.AddAgencyAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 3; i++)
        {
            await factory.AddPropertyAsync(agency.Id, $"Paginada {i}", $"Ciudad{marker}", PropertyStatus.Published);
        }

        var response = await factory.CreateClient().GetAsync($"{Url}?location={marker}&pageSize=2&page=2");
        var page = await response.ReadAsync<PagedResult<PublicPropertyDto>>();

        Assert.Equal(3, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal(2, page.TotalPages);
    }

    [Fact]
    public async Task TheAuthenticatedInventoryStillRequiresSession()
    {
        var response = await factory.CreateClient().GetAsync("/api/properties");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
