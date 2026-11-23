using System.Net;
using System.Security.Claims;
using System.Text;
using Alhecasa.Application.Auth;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Alhecasa.IntegrationTests;

/// <summary>
/// F0-02: un token firmado y vigente deja de servir en cuanto la cuenta cambia
/// (desactivada, eliminada, otro rol u otra inmobiliaria).
/// </summary>
public sealed class SessionRevalidationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Endpoint = "/api/auth/me";

    [Fact]
    public async Task UntouchedAccount_KeepsWorkingWithTheSameToken()
    {
        var agency = await factory.AddAgencyAsync();
        var user = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        var client = factory.CreateClientFor(user);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint)).StatusCode);
    }

    [Fact]
    public async Task DeactivatedUser_Gets401OnTheNextRequest()
    {
        var agency = await factory.AddAgencyAsync();
        var user = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        var client = factory.CreateClientFor(user);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint)).StatusCode);

        await factory.ModifyUserAsync(user.Id, u => u.Update(u.FirstName, u.LastName, u.Email, u.Role, u.AgencyId, isActive: false));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task DeletedUser_Gets401OnTheNextRequest()
    {
        var agency = await factory.AddAgencyAsync();
        var user = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        var client = factory.CreateClientFor(user);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);

        await factory.ModifyUserAsync(user.Id, u => u.MarkAsDeleted(DateTimeOffset.UtcNow));

        // /api/users no consulta a quien llama: solo la revalidación del token puede cerrar este acceso.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task UserWhoseRoleWasChanged_Gets401_NotAccessWithTheOldRole()
    {
        var agency = await factory.AddAgencyAsync();
        var admin = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        var client = factory.CreateClientFor(admin);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);

        await factory.ModifyUserAsync(admin.Id, u => u.Update(u.FirstName, u.LastName, u.Email, UserRole.Client, u.AgencyId, isActive: true));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task UserMovedToAnotherAgency_Gets401()
    {
        var first = await factory.AddAgencyAsync("First");
        var second = await factory.AddAgencyAsync("Second");
        var admin = await factory.AddUserAsync(UserRole.AgencyAdmin, first.Id);
        var client = factory.CreateClientFor(admin);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);

        await factory.ModifyUserAsync(admin.Id, u => u.Update(u.FirstName, u.LastName, u.Email, u.Role, second.Id, isActive: true));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task UserOfADeletedAgency_Gets401()
    {
        var agency = await factory.AddAgencyAsync();
        var admin = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        var client = factory.CreateClientFor(admin);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint)).StatusCode);

        await factory.ModifyAgencyAsync(agency.Id, a => a.MarkAsDeleted(DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
    }

    [Fact]
    public async Task ChangesToOtherUsers_DoNotAffectMySession()
    {
        var agency = await factory.AddAgencyAsync();
        var me = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        var other = await factory.AddUserAsync(UserRole.Client, agency.Id);
        var client = factory.CreateClientFor(me);

        await factory.ModifyUserAsync(other.Id, u => u.MarkAsDeleted(DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint)).StatusCode);
    }

    [Fact]
    public async Task ValidSignatureButUnknownUser_Gets401()
    {
        var agency = await factory.AddAgencyAsync();
        var ghost = new Alhecasa.Domain.Entities.User("Ghost", "User", "ghost@test.local", "x", UserRole.AgencyAdmin, agency.Id);
        var client = factory.CreateClientFor(ghost);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task TokenWithoutRoleClaim_Gets401()
    {
        var user = await factory.AddUserAsync(UserRole.Client, null);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", SignToken(new Claim(ClaimNames.Subject, user.Id.ToString())));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
    }

    [Fact]
    public async Task TokenWithMalformedAgencyClaim_Gets401()
    {
        var user = await factory.AddUserAsync(UserRole.Client, null);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", SignToken(
            new Claim(ClaimNames.Subject, user.Id.ToString()),
            new Claim(ClaimNames.Role, nameof(UserRole.Client)),
            new Claim(ClaimNames.AgencyId, "not-a-guid")));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_Gets401()
    {
        var user = await factory.AddUserAsync(UserRole.Client, null);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", SignToken(
            "a-completely-different-signing-key-0123456789",
            new Claim(ClaimNames.Subject, user.Id.ToString()),
            new Claim(ClaimNames.Role, nameof(UserRole.Client))));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
    }

    // Token con la firma y los datos de emisor correctos, para aislar la prueba en los claims.
    private static string SignToken(params Claim[] claims) =>
        SignToken("integration-tests-signing-key-0123456789-abcdef", claims);

    private static string SignToken(string key, params Claim[] claims) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = "alhecasa-api",
            Audience = "alhecasa-web",
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256)
        });
}
