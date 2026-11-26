using System.Net;
using Alhecasa.Application.Agencies;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.IntegrationTests;

/// <summary>La inmobiliaria configura su propia información (y logotipo) y la ve el público en su página.</summary>
public sealed class AgencyProfileEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string MeUrl = "/api/agencies/me";
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1, 2];

    private async Task<(Agency Agency, HttpClient Admin)> CreateAdminAsync(string name = "Inmobiliaria Norte")
    {
        var agency = await factory.AddAgencyAsync(name);
        var admin = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        return (agency, factory.CreateClientFor(admin));
    }

    private static UpdateAgencyProfileRequest Request(string name = "Inmobiliaria Norte SA", string? website = "https://norte.test") => new(
        name, "contacto@norte.test", "55 1234 5678", "Casas y departamentos.", website,
        "https://facebook.com/norte", "https://instagram.com/norte", "Lun a Vie 9 a 18",
        "Av. Reforma 100", "Ciudad de México", "CDMX", "06600");

    private static MultipartFormDataContent LogoForm(byte[] bytes, string type = "image/png", string name = "logo.png")
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new(type);
        form.Add(content, "file", name);
        return form;
    }

    private async Task<Agency> ReloadAsync(Guid id)
    {
        Agency? agency = null;
        await factory.WithDbAsync(async db => agency = await db.Agencies.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == id));
        return agency!;
    }

    // ---- Permisos ----

    [Fact]
    public async Task Anonymous_IsRejected()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MeUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutJsonAsync(MeUrl, Request())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsync($"{MeUrl}/logo", LogoForm(Png))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync($"{MeUrl}/logo")).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Seller)]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Client)]
    public async Task EmployeesAndClients_CannotUseIt(UserRole role)
    {
        var agency = await factory.AddAgencyAsync("Norte");
        var user = await factory.AddUserAsync(role, role == UserRole.Client ? null : agency.Id);
        var client = factory.CreateClientFor(user);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(MeUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutJsonAsync(MeUrl, Request())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync($"{MeUrl}/logo", LogoForm(Png))).StatusCode);
        Assert.Equal("Norte", (await ReloadAsync(agency.Id)).Name);
    }

    [Fact]
    public async Task ThePlatformAdmin_HasNoAgencyOfItsOwn()
    {
        var client = factory.CreateClientFor(await factory.AddUserAsync(UserRole.PlatformAdmin, null));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(MeUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutJsonAsync(MeUrl, Request())).StatusCode);
    }

    [Fact]
    public async Task AFreeIndividualAccount_CannotEditAnAgencyProfile()
    {
        var individual = Agency.CreateIndividual("Ana López", $"{Guid.NewGuid():N}@individual.test");
        await factory.WithDbAsync(async db =>
        {
            db.Agencies.Add(individual);
            await db.SaveChangesAsync();
        });
        var client = factory.CreateClientFor(await factory.AddUserAsync(UserRole.AgencyAdmin, individual.Id));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(MeUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutJsonAsync(MeUrl, Request())).StatusCode);
        Assert.Equal("Ana López", (await ReloadAsync(individual.Id)).Name);
    }

    // ---- Lectura y edición ----

    [Fact]
    public async Task TheAdmin_ReadsAndEditsOnlyItsOwnAgency()
    {
        var (agency, admin) = await CreateAdminAsync("Norte");
        var other = await factory.AddAgencyAsync("Sur");

        var before = await (await admin.GetAsync(MeUrl)).ReadAsync<AgencyProfileDto>();
        Assert.Equal(agency.Id, before.Id);
        Assert.Equal("Norte", before.Name);

        var response = await admin.PutJsonAsync(MeUrl, Request("Norte Renovada"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<AgencyProfileDto>();

        Assert.Equal("Norte Renovada", updated.Name);
        Assert.Equal("Av. Reforma 100", updated.Street);
        var saved = await ReloadAsync(agency.Id);
        Assert.Equal("Norte Renovada", saved.Name);
        Assert.Equal("https://norte.test", saved.Website);
        Assert.Equal("Lun a Vie 9 a 18", saved.OpeningHours);
        Assert.Equal("Sur", (await ReloadAsync(other.Id)).Name); // la ajena no se toca
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://norte.test")]
    [InlineData("norte.test")]
    public async Task Update_RejectsUnsafeWebAddresses_With400AndSavesNothing(string url)
    {
        var (agency, admin) = await CreateAdminAsync("Norte");

        var response = await admin.PutJsonAsync(MeUrl, Request(website: url));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var saved = await ReloadAsync(agency.Id);
        Assert.Equal("Norte", saved.Name);
        Assert.Null(saved.Website);
    }

    [Fact]
    public async Task Update_RequiresNameAndAValidEmail()
    {
        var (_, admin) = await CreateAdminAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutJsonAsync(MeUrl, Request(name: " "))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutJsonAsync(MeUrl, Request() with { Email = "no" })).StatusCode);
    }

    // ---- Logotipo ----

    [Fact]
    public async Task Logo_CanBeUploadedReplacedAndRemoved()
    {
        var (agency, admin) = await CreateAdminAsync();

        var up = await admin.PutAsync($"{MeUrl}/logo", LogoForm(Png));
        Assert.Equal(HttpStatusCode.OK, up.StatusCode);
        Assert.True((await up.ReadAsync<AgencyProfileDto>()).HasLogo);
        var first = (await ReloadAsync(agency.Id)).LogoStorageKey!;
        Assert.True(factory.Storage.Contains(first));

        var replace = await admin.PutAsync($"{MeUrl}/logo", LogoForm(ApiFactory.JpegBytes, "image/jpeg", "logo.jpg"));
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        var second = (await ReloadAsync(agency.Id)).LogoStorageKey!;
        Assert.NotEqual(first, second);
        Assert.False(factory.Storage.Contains(first));
        Assert.True(factory.Storage.Contains(second));

        var removed = await admin.DeleteAsync($"{MeUrl}/logo");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.False((await removed.ReadAsync<AgencyProfileDto>()).HasLogo);
        Assert.False(factory.Storage.Contains(second));
    }

    [Fact]
    public async Task Logo_RejectsFilesThatAreNotImages_EvenIfTheyClaimToBe()
    {
        var (agency, admin) = await CreateAdminAsync();

        var svg = "<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>"u8.ToArray();
        var response = await admin.PutAsync($"{MeUrl}/logo", LogoForm(svg, "image/png", "logo.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False((await ReloadAsync(agency.Id)).HasLogo);
    }

    [Fact]
    public async Task Logo_WithoutAFile_Is400()
    {
        var (_, admin) = await CreateAdminAsync();

        var response = await admin.PutAsync($"{MeUrl}/logo", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Lo que ve el público ----

    [Fact]
    public async Task Public_ShowsWhatTheAdminConfigured_WithoutASession()
    {
        var (agency, admin) = await CreateAdminAsync();
        await admin.PutJsonAsync(MeUrl, Request("Norte Pública"));

        var response = await factory.CreateClient().GetAsync($"/api/public/agencies/{agency.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.ReadAsync<PublicAgencyDto>();
        Assert.Equal("Norte Pública", dto.Name);
        Assert.Equal("Casas y departamentos.", dto.Description);
        Assert.Equal("https://instagram.com/norte", dto.InstagramUrl);
        Assert.Equal("Ciudad de México", dto.City);
        Assert.False(dto.HasLogo);
    }

    [Fact]
    public async Task Public_NeverShowsIndividualsInactiveOrUnknownAgencies()
    {
        var individual = Agency.CreateIndividual("Ana López", $"{Guid.NewGuid():N}@individual.test");
        var inactive = await factory.AddAgencyAsync("Inactiva");
        await factory.WithDbAsync(async db =>
        {
            db.Agencies.Add(individual);
            await db.SaveChangesAsync();
        });
        await factory.ModifyAgencyAsync(inactive.Id, a => typeof(Agency).GetProperty(nameof(Agency.IsActive))!.SetValue(a, false));
        var client = factory.CreateClient();

        foreach (var id in new[] { individual.Id, inactive.Id, Guid.NewGuid() })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/public/agencies/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/public/agencies/{id}/logo")).StatusCode);
        }
    }

    [Fact]
    public async Task Public_Logo_IsServedWithItsType_AndRevalidatedWithAnETag()
    {
        var (agency, admin) = await CreateAdminAsync();
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/public/agencies/{agency.Id}/logo")).StatusCode);

        await admin.PutAsync($"{MeUrl}/logo", LogoForm(Png));

        var response = await client.GetAsync($"/api/public/agencies/{agency.Id}/logo");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync());
        var etag = response.Headers.ETag;
        Assert.NotNull(etag);

        var again = new HttpRequestMessage(HttpMethod.Get, $"/api/public/agencies/{agency.Id}/logo");
        again.Headers.IfNoneMatch.Add(etag);
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(again)).StatusCode);

        // Al cambiar el logotipo cambia el ETag: el navegador descarga el nuevo.
        await admin.PutAsync($"{MeUrl}/logo", LogoForm(ApiFactory.JpegBytes, "image/jpeg", "logo.jpg"));
        var changed = await client.SendAsync(Clone(again, etag));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal("image/jpeg", changed.Content.Headers.ContentType?.MediaType);
    }

    private static HttpRequestMessage Clone(HttpRequestMessage original, System.Net.Http.Headers.EntityTagHeaderValue etag)
    {
        var copy = new HttpRequestMessage(HttpMethod.Get, original.RequestUri);
        copy.Headers.IfNoneMatch.Add(etag);
        return copy;
    }

    [Fact]
    public async Task TheNewName_IsWhatThePublicCatalogShowsAsPublisher()
    {
        var (agency, admin) = await CreateAdminAsync("Nombre viejo");
        var property = await factory.AddPropertyAsync(agency.Id, "Casa", status: PropertyStatus.Published);

        await admin.PutJsonAsync(MeUrl, Request("Nombre nuevo"));

        var detail = await (await factory.CreateClient().GetAsync($"/api/public/properties/{property.Id}"))
            .ReadAsync<Alhecasa.Application.Properties.PublicPropertyDto>();
        Assert.Equal("Nombre nuevo", detail.AgencyName);
    }
}