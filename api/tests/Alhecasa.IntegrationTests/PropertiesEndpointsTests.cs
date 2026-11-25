using System.Net;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.IntegrationTests;

/// <summary>Inventario de propiedades: permisos por rol, aislamiento por inmobiliaria y ciclo de alta, edición y baja.</summary>
public sealed class PropertiesEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/properties";

    private sealed record Tenant(Agency Agency, User Admin, User Supervisor, User Seller);

    private async Task<Tenant> CreateTenantAsync(string name = "Agency")
    {
        var agency = await factory.AddAgencyAsync(name);
        return new Tenant(
            agency,
            await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id),
            await factory.AddUserAsync(UserRole.Supervisor, agency.Id),
            await factory.AddUserAsync(UserRole.Seller, agency.Id));
    }

    private static CreatePropertyRequest NewRequest(Guid? agencyId = null, string title = "Casa en Polanco") => new()
    {
        AgencyId = agencyId,
        Title = title,
        Description = "Amplia, con jardín",
        Type = PropertyType.House,
        Operation = OperationType.Sale,
        Status = PropertyStatus.Draft,
        Price = 4_500_000m,
        Currency = CurrencyCode.MXN,
        Street = "Calle Horacio 100",
        Neighborhood = "Polanco",
        City = "Ciudad de México",
        State = "CDMX",
        PostalCode = "11560",
        Bedrooms = 3,
        Bathrooms = 2,
        ConstructionArea = 250.5m,
        LandArea = 300m,
        Latitude = 19.4326077m,
        Longitude = -99.1332079m
    };

    private static UpdatePropertyRequest UpdateRequest(string title) => new()
    {
        Title = title,
        Type = PropertyType.Apartment,
        Operation = OperationType.Rent,
        Status = PropertyStatus.Published,
        Price = 25_000m,
        Currency = CurrencyCode.USD,
        City = "Monterrey",
        State = "NL"
    };

    // --- Acceso -----------------------------------------------------------------------------

    [Fact]
    public async Task WithoutToken_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task Client_Returns403()
    {
        var tenant = await CreateTenantAsync();
        var client = await factory.AddUserAsync(UserRole.Client, tenant.Agency.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await factory.CreateClientFor(client).GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.CreateClientFor(client).PostJsonAsync(Url, NewRequest())).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.AgencyAdmin)]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Seller)]
    public async Task AgencyStaff_CanListAndCreate(UserRole role)
    {
        var tenant = await CreateTenantAsync();
        var staff = role switch
        {
            UserRole.AgencyAdmin => tenant.Admin,
            UserRole.Supervisor => tenant.Supervisor,
            _ => tenant.Seller
        };
        var client = factory.CreateClientFor(staff);

        var created = await client.PostJsonAsync(Url, NewRequest());

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Url)).StatusCode);
    }

    // --- Alta -------------------------------------------------------------------------------

    [Fact]
    public async Task Create_PersistsEveryField_AndReturnsLocation()
    {
        var tenant = await CreateTenantAsync("Demo Agency");

        var response = await factory.CreateClientFor(tenant.Seller).PostJsonAsync(Url, NewRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var property = await response.ReadAsync<PropertyDto>();
        Assert.Equal($"{Url}/{property.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(tenant.Agency.Id, property.AgencyId);
        Assert.Equal("Demo Agency", property.AgencyName);
        Assert.Equal("Casa en Polanco", property.Title);
        Assert.Equal(PropertyType.House, property.Type);
        Assert.Equal(OperationType.Sale, property.Operation);
        Assert.Equal(4_500_000m, property.Price);
        Assert.Equal(250.5m, property.ConstructionArea);
        Assert.Equal(19.432608m, property.Latitude);
        Assert.Equal(-99.133208m, property.Longitude);
        Assert.Equal(3, property.Bedrooms);
        Assert.True(property.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Theory]
    [InlineData(91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    [InlineData(19.4, null)]
    [InlineData(null, -99.1)]
    public async Task Create_WithInvalidCoordinates_Returns400(double? latitude, double? longitude)
    {
        var tenant = await CreateTenantAsync();
        var request = NewRequest() with { Latitude = (decimal?)latitude, Longitude = (decimal?)longitude };

        var response = await factory.CreateClientFor(tenant.Seller).PostJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ForAnotherAgency_Returns403()
    {
        var a = await CreateTenantAsync("A");
        var b = await CreateTenantAsync("B");

        var response = await factory.CreateClientFor(a.Seller).PostJsonAsync(Url, NewRequest(b.Agency.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlatformAdmin_CreatesForAnAgency_ButNotWithoutOne()
    {
        var tenant = await CreateTenantAsync();
        var admin = await factory.AddUserAsync(UserRole.PlatformAdmin, null);
        var client = factory.CreateClientFor(admin);

        var withAgency = await client.PostJsonAsync(Url, NewRequest(tenant.Agency.Id));
        var withoutAgency = await client.PostJsonAsync(Url, NewRequest());

        Assert.Equal(HttpStatusCode.Created, withAgency.StatusCode);
        Assert.Equal(tenant.Agency.Id, (await withAgency.ReadAsync<PropertyDto>()).AgencyId);
        Assert.Equal(HttpStatusCode.BadRequest, withoutAgency.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidData_Returns400WithFieldErrors()
    {
        var tenant = await CreateTenantAsync();
        var request = NewRequest() with { Title = "", Price = 0, City = "", Type = 0 };

        var response = await factory.CreateClientFor(tenant.Seller).PostJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        foreach (var field in new[] { "Title", "Price", "City", "Type" })
        {
            Assert.Contains(field, body, StringComparison.Ordinal);
        }
    }

    // --- Aislamiento ------------------------------------------------------------------------

    [Fact]
    public async Task List_ShowsOnlyOwnAgencyInventory()
    {
        var a = await CreateTenantAsync("A");
        var b = await CreateTenantAsync("B");
        var own = await factory.AddPropertyAsync(a.Agency.Id, "Propia");
        var foreign = await factory.AddPropertyAsync(b.Agency.Id, "Ajena");

        var page = await (await factory.CreateClientFor(a.Seller).GetAsync($"{Url}?pageSize=100"))
            .ReadAsync<PagedResult<PropertyDto>>();

        var ids = page.Items.Select(p => p.Id).ToList();
        Assert.Contains(own.Id, ids);
        Assert.DoesNotContain(foreign.Id, ids);
    }

    [Fact]
    public async Task PlatformAdmin_SeesEveryAgencyInventory()
    {
        var a = await CreateTenantAsync("A");
        var b = await CreateTenantAsync("B");
        var inA = await factory.AddPropertyAsync(a.Agency.Id);
        var inB = await factory.AddPropertyAsync(b.Agency.Id);
        var admin = await factory.AddUserAsync(UserRole.PlatformAdmin, null);

        var page = await (await factory.CreateClientFor(admin).GetAsync($"{Url}?pageSize=100"))
            .ReadAsync<PagedResult<PropertyDto>>();

        Assert.Contains(inA.Id, page.Items.Select(p => p.Id));
        Assert.Contains(inB.Id, page.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task OtherAgencyProperty_Returns404_ForReadUpdateAndDelete_AndStaysUntouched()
    {
        var a = await CreateTenantAsync("A");
        var b = await CreateTenantAsync("B");
        var foreign = await factory.AddPropertyAsync(b.Agency.Id, "Ajena");
        var client = factory.CreateClientFor(a.Admin);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Url}/{foreign.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutJsonAsync($"{Url}/{foreign.Id}", UpdateRequest("Hackeada"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{Url}/{foreign.Id}")).StatusCode);

        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Properties.IgnoreQueryFilters().SingleAsync(p => p.Id == foreign.Id);
            Assert.Equal("Ajena", stored.Title);
            Assert.False(stored.IsDeleted);
        });
    }

    // --- Búsqueda y paginación --------------------------------------------------------------

    [Fact]
    public async Task List_SearchFiltersByTitleAndCity_AndPaginates()
    {
        var tenant = await CreateTenantAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        await factory.AddPropertyAsync(tenant.Agency.Id, $"Depa {marker} uno", "Guadalajara");
        await factory.AddPropertyAsync(tenant.Agency.Id, $"Depa {marker} dos", "Guadalajara");
        await factory.AddPropertyAsync(tenant.Agency.Id, "Sin relación", "Puebla");
        var client = factory.CreateClientFor(tenant.Seller);

        var byTitle = await (await client.GetAsync($"{Url}?search={marker}&pageSize=1")).ReadAsync<PagedResult<PropertyDto>>();
        var byCity = await (await client.GetAsync($"{Url}?search=Guadalajara")).ReadAsync<PagedResult<PropertyDto>>();

        Assert.Equal(2, byTitle.TotalCount);
        Assert.Single(byTitle.Items);
        Assert.Equal(2, byTitle.TotalPages);
        Assert.Equal(2, byCity.TotalCount);
    }

    [Fact]
    public async Task List_SearchTreatsWildcardsAsText()
    {
        var tenant = await CreateTenantAsync();
        await factory.AddPropertyAsync(tenant.Agency.Id, "Casa 100% nueva");
        await factory.AddPropertyAsync(tenant.Agency.Id, "Casa usada");
        var client = factory.CreateClientFor(tenant.Seller);

        var page = await (await client.GetAsync($"{Url}?search=%25")).ReadAsync<PagedResult<PropertyDto>>();

        Assert.Equal(["Casa 100% nueva"], page.Items.Select(p => p.Title));
    }

    [Fact]
    public async Task List_ShowsNewestFirst()
    {
        var tenant = await CreateTenantAsync();
        var client = factory.CreateClientFor(tenant.Seller);
        await client.PostJsonAsync(Url, NewRequest(title: "Primera"));
        await Task.Delay(20);
        await client.PostJsonAsync(Url, NewRequest(title: "Segunda"));

        var page = await (await client.GetAsync(Url)).ReadAsync<PagedResult<PropertyDto>>();

        Assert.Equal(["Segunda", "Primera"], page.Items.Select(p => p.Title));
    }

    // --- Edición y baja ---------------------------------------------------------------------

    [Fact]
    public async Task Update_ChangesTheProperty_AndSetsUpdatedAt()
    {
        var tenant = await CreateTenantAsync();
        var existing = await factory.AddPropertyAsync(tenant.Agency.Id);
        await factory.AddImagesAsync(existing.Id);

        var response = await factory.CreateClientFor(tenant.Seller).PutJsonAsync($"{Url}/{existing.Id}", UpdateRequest("Publicada"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<PropertyDto>();
        Assert.Equal("Publicada", updated.Title);
        Assert.Equal(PropertyStatus.Published, updated.Status);
        Assert.Equal(OperationType.Rent, updated.Operation);
        Assert.Equal(CurrencyCode.USD, updated.Currency);
        Assert.Equal(tenant.Agency.Id, updated.AgencyId);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_WithInvalidData_Returns400_AndKeepsTheProperty()
    {
        var tenant = await CreateTenantAsync();
        var existing = await factory.AddPropertyAsync(tenant.Agency.Id, "Original");

        var response = await factory.CreateClientFor(tenant.Seller)
            .PutJsonAsync($"{Url}/{existing.Id}", UpdateRequest("Cambio") with { Price = -5 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var stored = await (await factory.CreateClientFor(tenant.Seller).GetAsync($"{Url}/{existing.Id}")).ReadAsync<PropertyDto>();
        Assert.Equal("Original", stored.Title);
    }

    [Fact]
    public async Task Delete_IsSoft_ThenPropertyIsGone()
    {
        var tenant = await CreateTenantAsync();
        var existing = await factory.AddPropertyAsync(tenant.Agency.Id);
        var client = factory.CreateClientFor(tenant.Supervisor);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Url}/{existing.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Url}/{existing.Id}")).StatusCode);
        var page = await (await client.GetAsync($"{Url}?pageSize=100")).ReadAsync<PagedResult<PropertyDto>>();
        Assert.DoesNotContain(existing.Id, page.Items.Select(p => p.Id));
        await factory.WithDbAsync(async db =>
        {
            var row = await db.Properties.IgnoreQueryFilters().SingleAsync(p => p.Id == existing.Id);
            Assert.True(row.IsDeleted);
            Assert.NotNull(row.DeletedAt);
        });
    }

    [Fact]
    public async Task NonGuidId_Returns404()
    {
        var tenant = await CreateTenantAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClientFor(tenant.Seller).GetAsync($"{Url}/abc")).StatusCode);
    }
}
