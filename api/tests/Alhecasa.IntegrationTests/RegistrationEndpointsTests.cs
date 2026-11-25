using System.Net;
using System.Net.Http.Json;
using Alhecasa.Application.Agencies;
using Alhecasa.Application.Auth;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.IntegrationTests;

/// <summary>Cuenta gratuita de un particular: registro público, espacio propio, tope de propiedades y límites de acceso.</summary>
public sealed class RegistrationEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/auth/register";
    private const string StrongPassword = "Correct-Horse-9";

    private static RegisterRequest NewRequest(string? email = null, string password = StrongPassword) =>
        new("Ana", "López", email ?? $"{Guid.NewGuid():N}@nuevo.test", password);

    private static CreatePropertyRequest NewProperty(string title = "Mi casa") => new()
    {
        Title = title,
        Type = PropertyType.House,
        Operation = OperationType.Sale,
        Status = PropertyStatus.Draft,
        Price = 2_000_000m,
        Currency = CurrencyCode.MXN,
        City = "Ciudad de México",
        State = "CDMX"
    };

    private async Task<(HttpClient Client, LoginResponse Login)> RegisterAsync(string? email = null)
    {
        var response = await factory.CreateClient().PostJsonAsync(Url, NewRequest(email));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var login = await response.ReadAsync<LoginResponse>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        return (client, login);
    }

    [Fact]
    public async Task ARegisteredVisitor_GetsASession_AnIndividualSpace_AndTheAgencyAdminRole()
    {
        var request = NewRequest("Ana@Nuevo.Test");

        var response = await factory.CreateClient().PostJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var login = await response.ReadAsync<LoginResponse>();
        Assert.NotEmpty(login.AccessToken);
        Assert.Equal("ana@nuevo.test", login.User.Email);
        Assert.Equal(UserRole.AgencyAdmin, login.User.Role);
        Assert.True(login.User.IsIndividual);
        Assert.NotNull(login.User.AgencyId);

        await factory.WithDbAsync(async db =>
        {
            var agency = await db.Agencies.IgnoreQueryFilters().SingleAsync(a => a.Id == login.User.AgencyId);
            Assert.Equal(AgencyKind.Individual, agency.Kind);
            Assert.Equal("Ana López", agency.Name);
            var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == login.User.Id);
            Assert.NotEqual(StrongPassword, user.PasswordHash);
            Assert.Equal(agency.Id, user.AgencyId);
        });
    }

    [Fact]
    public async Task TheNewAccount_CanLogInLater_WithItsPassword()
    {
        var (_, registered) = await RegisterAsync();

        var response = await factory.CreateClient().PostJsonAsync("/api/auth/login", new LoginRequest(registered.User.Email, StrongPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.ReadAsync<LoginResponse>();
        Assert.True(login.User.IsIndividual);
        Assert.Equal(registered.User.AgencyId, login.User.AgencyId);
    }

    [Fact]
    public async Task TheSessionReturnedByTheRegistration_WorksRightAway()
    {
        var (client, login) = await RegisterAsync();

        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var user = await me.ReadAsync<CurrentUserDto>();
        Assert.Equal(login.User.Id, user.Id);
        Assert.True(user.IsIndividual);
    }

    [Fact]
    public async Task TheBodyCannotChooseTheRoleNorTheAgency()
    {
        var other = await factory.AddAgencyAsync();
        var body = new
        {
            firstName = "Ana",
            lastName = "López",
            email = $"{Guid.NewGuid():N}@nuevo.test",
            password = StrongPassword,
            role = "PlatformAdmin",
            agencyId = other.Id,
            isActive = false
        };

        var response = await factory.CreateClient().PostJsonAsync(Url, body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var login = await response.ReadAsync<LoginResponse>();
        Assert.Equal(UserRole.AgencyAdmin, login.User.Role);
        Assert.NotEqual(other.Id, login.User.AgencyId);
    }

    [Fact]
    public async Task ARegisteredEmail_Returns409_AndKeepsTheOriginalAccount()
    {
        var (_, first) = await RegisterAsync("repetido@nuevo.test");

        var response = await factory.CreateClient().PostJsonAsync(Url, NewRequest("REPETIDO@nuevo.test"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await factory.WithDbAsync(async db =>
            Assert.Equal(1, await db.Users.IgnoreQueryFilters().CountAsync(u => u.Email == "repetido@nuevo.test" && u.Id == first.User.Id)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("corta1A")]
    [InlineData("sinmayusculas123")]
    public async Task AWeakPassword_Returns400_AndCreatesNothing(string password)
    {
        var request = NewRequest(password: password);

        var response = await factory.CreateClient().PostJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await factory.WithDbAsync(async db => Assert.False(await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == request.Email)));
    }

    [Fact]
    public async Task AnInvalidEmail_Returns400()
    {
        var response = await factory.CreateClient().PostJsonAsync(Url, NewRequest("no-es-correo"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheOwner_CreatesPropertiesInItsOwnSpace_AndSeesOnlyThose()
    {
        var agency = await factory.AddAgencyAsync();
        await factory.AddPropertyAsync(agency.Id, "De una inmobiliaria");
        var (client, login) = await RegisterAsync();

        var created = await client.PostJsonAsync("/api/properties", NewProperty("Mi departamento"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(login.User.AgencyId, (await created.ReadAsync<PropertyDto>()).AgencyId);

        var list = await (await client.GetAsync("/api/properties")).ReadAsync<PagedResult<PropertyDto>>();
        Assert.Equal(["Mi departamento"], list.Items.Select(p => p.Title));
    }

    [Fact]
    public async Task TheFreeAccount_Returns422WithAReadableMessage_AtTheFourthProperty()
    {
        var (client, _) = await RegisterAsync();
        for (var i = 1; i <= 3; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostJsonAsync("/api/properties", NewProperty($"Casa {i}"))).StatusCode);
        }

        var response = await client.PostJsonAsync("/api/properties", NewProperty("Casa 4"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("3 propiedades", problem.Detail);
        var list = await (await client.GetAsync("/api/properties")).ReadAsync<PagedResult<PropertyDto>>();
        Assert.Equal(3, list.TotalCount);
    }

    [Fact]
    public async Task ARegularAgency_IsNotLimited()
    {
        var agency = await factory.AddAgencyAsync();
        var admin = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        var client = factory.CreateClientFor(admin);

        for (var i = 1; i <= 5; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostJsonAsync("/api/properties", NewProperty($"Casa {i}"))).StatusCode);
        }
    }

    [Fact]
    public async Task TheOwner_CannotManageUsers_NorSeeOtherAgencies()
    {
        await factory.AddAgencyAsync("Otra inmobiliaria");
        var (client, login) = await RegisterAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        var created = await client.PostJsonAsync("/api/users", new { firstName = "A", lastName = "B", email = "x@y.test", password = StrongPassword, role = "Seller" });
        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);

        var agencies = await (await client.GetAsync("/api/agencies")).ReadAsync<List<AgencyDto>>();
        var own = Assert.Single(agencies);
        Assert.Equal(login.User.AgencyId, own.Id);
        Assert.True(own.IsIndividual);
    }

    [Fact]
    public async Task APublishedPropertyOfAFreeAccount_IsShownAsParticular_NeverWithTheRealName()
    {
        var (_, login) = await RegisterAsync();
        var property = await factory.AddPropertyAsync(login.User.AgencyId!.Value, "Depa de particular", status: PropertyStatus.Published);
        var anonymous = factory.CreateClient();

        var list = await (await anonymous.GetAsync("/api/public/properties?pageSize=100")).ReadAsync<PagedResult<PublicPropertyDto>>();
        var detail = await (await anonymous.GetAsync($"/api/public/properties/{property.Id}")).ReadAsync<PublicPropertyDto>();

        Assert.Equal("Particular", Assert.Single(list.Items, p => p.Id == property.Id).AgencyName);
        Assert.Equal("Particular", detail.AgencyName);
        Assert.DoesNotContain(login.User.FullName, await (await anonymous.GetAsync($"/api/public/properties/{property.Id}")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheOwner_ReceivesTheInquiriesOfItsProperties_AndNoOthers()
    {
        var other = await factory.AddAgencyAsync();
        var otherProperty = await factory.AddPropertyAsync(other.Id, status: PropertyStatus.Published);
        await factory.AddInquiryAsync(other.Id, otherProperty.Id, name: "De otra");
        var (client, login) = await RegisterAsync();
        var own = await factory.AddPropertyAsync(login.User.AgencyId!.Value, "Mi casa", status: PropertyStatus.Published);
        await factory.AddInquiryAsync(login.User.AgencyId!.Value, own.Id, name: "Mi prospecto");

        var list = await (await client.GetAsync("/api/inquiries")).ReadAsync<PagedResult<Alhecasa.Application.Inquiries.InquiryDto>>();

        Assert.Equal(["Mi prospecto"], list.Items.Select(i => i.Name));
    }
}

/// <summary>El alta pública crea un espacio y un usuario por solicitud: pocas por IP.</summary>
public sealed class RegistrationRateLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task AfterFiveRegistrationsFromTheSameIp_TheNextOneIsRejectedWith429()
    {
        var client = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:Register:PermitLimit", "5")).CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var ok = await client.PostJsonAsync("/api/auth/register", new RegisterRequest("Ana", "López", $"{Guid.NewGuid():N}@nuevo.test", "Correct-Horse-9"));
            Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        }

        var limited = await client.PostJsonAsync("/api/auth/register", new RegisterRequest("Ana", "López", $"{Guid.NewGuid():N}@nuevo.test", "Correct-Horse-9"));

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
}