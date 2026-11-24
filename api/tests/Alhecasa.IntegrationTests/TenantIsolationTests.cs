using System.Net;
using Alhecasa.Application.Agencies;
using Alhecasa.Application.Common;
using Alhecasa.Application.Users;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.IntegrationTests;

/// <summary>Aislamiento por inmobiliaria y reglas de permisos de /api/users y /api/agencies.</summary>
public sealed class TenantIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record Tenant(Agency Agency, User Admin, User Member);

    private async Task<Tenant> CreateTenantAsync(string name)
    {
        var agency = await factory.AddAgencyAsync(name);
        var admin = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        var member = await factory.AddUserAsync(UserRole.Client, agency.Id);
        return new Tenant(agency, admin, member);
    }

    private static CreateUserRequest NewUserRequest(UserRole role, Guid? agencyId, string? email = null) =>
        new("New", "Person", email ?? $"{Guid.NewGuid():N}@test.local", ApiFactory.UserPassword, role, agencyId);

    // --- Lectura ----------------------------------------------------------------------------

    [Fact]
    public async Task AgencyAdmin_ListsOnlyUsersOfOwnAgency()
    {
        var a = await CreateTenantAsync("A");
        var b = await CreateTenantAsync("B");
        var platformAdmin = await factory.AddUserAsync(UserRole.PlatformAdmin, null);

        var response = await factory.CreateClientFor(a.Admin).GetAsync("/api/users?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = (await response.ReadAsync<PagedResult<UserDto>>()).Items.Select(u => u.Id).ToList();
        Assert.Contains(a.Admin.Id, ids);
        Assert.Contains(a.Member.Id, ids);
        Assert.DoesNotContain(b.Admin.Id, ids);
        Assert.DoesNotContain(b.Member.Id, ids);
        Assert.DoesNotContain(platformAdmin.Id, ids);
    }

    [Fact]
    public async Task AgencyAdmin_GettingUserOfAnotherAgency_Returns404NotForbidden()
    {
        var a = await CreateTenantAsync("A");
        var b = await CreateTenantAsync("B");

        var response = await factory.CreateClientFor(a.Admin).GetAsync($"/api/users/{b.Member.Id}");

        // 404 y no 403: no se confirma que el recurso exista en otra inmobiliaria.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(404, (await response.ReadProblemAsync()).Status);
    }

    [Fact]
    public async Task AgencyAdmin_CannotSeePlatformAdmin()
    {
        var a = await CreateTenantAsync("A");
        var platformAdmin = await factory.AddUserAsync(UserRole.PlatformAdmin, null);

        var response = await factory.CreateClientFor(a.Admin).GetAsync($"/api/users/{platformAdmin.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PlatformAdmin_SeesUsersOfEveryAgency()
    {
        var a = await CreateTenantAsync("A");
        var b = await CreateTenantAsync("B");
        var platformAdmin = await factory.AddUserAsync(UserRole.PlatformAdmin, null);
        var client = factory.CreateClientFor(platformAdmin);

        var list = await (await client.GetAsync("/api/users?pageSize=100")).ReadAsync<PagedResult<UserDto>>();
        var ids = list.Items.Select(u => u.Id).ToList();

        Assert.Contains(a.Member.Id, ids);
        Assert.Contains(b.Member.Id, ids);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/users/{b.Member.Id}")).StatusCode);
    }

    [Fact]
    public async Task Agencies_AgencyAdminSeesOnlyOwn_PlatformAdminSeesAll()
    {
        var a = await CreateTenantAsync("A");
        var b = await CreateTenantAsync("B");
        var platformAdmin = await factory.AddUserAsync(UserRole.PlatformAdmin, null);

        var own = await (await factory.CreateClientFor(a.Admin).GetAsync("/api/agencies"))
            .ReadAsync<List<AgencyDto>>();
        var all = await (await factory.CreateClientFor(platformAdmin).GetAsync("/api/agencies"))
            .ReadAsync<List<AgencyDto>>();

        Assert.Equal([a.Agency.Id], own.Select(x => x.Id));
        Assert.Contains(a.Agency.Id, all.Select(x => x.Id));
        Assert.Contains(b.Agency.Id, all.Select(x => x.Id));
    }

    // --- Permisos (403) ---------------------------------------------------------------------

    [Theory]
    [InlineData("/api/users")]
    [InlineData("/api/agencies")]
    public async Task Client_Gets403_OnAdministrationEndpoints(string url)
    {
        var a = await CreateTenantAsync("A");

        var response = await factory.CreateClientFor(a.Member).GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AgencyAdmin_CreatingUserInAnotherAgency_Returns403()
    {
        var a = await CreateTenantAsync("A");
        var b = await CreateTenantAsync("B");

        var response = await factory.CreateClientFor(a.Admin)
            .PostJsonAsync("/api/users", NewUserRequest(UserRole.AgencyAdmin, b.Agency.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AgencyAdmin_CreatingPlatformAdmin_Returns403()
    {
        var a = await CreateTenantAsync("A");

        var response = await factory.CreateClientFor(a.Admin)
            .PostJsonAsync("/api/users", NewUserRequest(UserRole.PlatformAdmin, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AgencyAdmin_DeletingYourself_Returns403()
    {
        var a = await CreateTenantAsync("A");

        var response = await factory.CreateClientFor(a.Admin).DeleteAsync($"/api/users/{a.Admin.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- Escritura sobre otra inmobiliaria (404) --------------------------------------------

    [Fact]
    public async Task AgencyAdmin_UpdatingUserOfAnotherAgency_Returns404_AndLeavesItUntouched()
    {
        var a = await CreateTenantAsync("A");
        var b = await CreateTenantAsync("B");
        var request = new UpdateUserRequest("Hacked", "User", b.Member.Email, UserRole.Client, b.Agency.Id, true);

        var response = await factory.CreateClientFor(a.Admin).PutJsonAsync($"/api/users/{b.Member.Id}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == b.Member.Id);
            Assert.Equal("Test", stored.FirstName);
        });
    }

    [Fact]
    public async Task AgencyAdmin_DeletingUserOfAnotherAgency_Returns404_AndKeepsIt()
    {
        var a = await CreateTenantAsync("A");
        var b = await CreateTenantAsync("B");

        var response = await factory.CreateClientFor(a.Admin).DeleteAsync($"/api/users/{b.Member.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await factory.WithDbAsync(async db =>
            Assert.False((await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == b.Member.Id)).IsDeleted));
    }

    // --- Casos felices y errores de negocio -------------------------------------------------

    [Fact]
    public async Task AgencyAdmin_CreatingUserWithoutAgencyId_PlacesItInOwnAgency()
    {
        var a = await CreateTenantAsync("A");
        var client = factory.CreateClientFor(a.Admin);

        var response = await client.PostJsonAsync("/api/users", NewUserRequest(UserRole.Seller, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<UserDto>();
        Assert.Equal(a.Agency.Id, created.AgencyId);
        Assert.Equal($"/api/users/{created.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/users/{created.Id}")).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Seller)]
    public async Task AgencyAdmin_CreatesEmployee_WhoCanLogInButCannotManageUsers(UserRole role)
    {
        var a = await CreateTenantAsync("A");
        var email = $"{Guid.NewGuid():N}@test.local";

        var created = await factory.CreateClientFor(a.Admin)
            .PostJsonAsync("/api/users", NewUserRequest(role, null, email));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var employee = await created.ReadAsync<UserDto>();
        Assert.Equal(role, employee.Role);
        Assert.Equal(a.Agency.Id, employee.AgencyId);

        var client = factory.CreateClient();
        var login = await client.PostJsonAsync("/api/auth/login", new Alhecasa.Application.Auth.LoginRequest(email, ApiFactory.UserPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.ReadAsync<Alhecasa.Application.Auth.LoginResponse>();
        Assert.Equal(role, session.User.Role);
        Assert.Equal(a.Agency.Id, session.User.AgencyId);

        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/agencies")).StatusCode);
    }

    [Fact]
    public async Task AgencyAdmin_CreatingClient_Returns403_AndCreatesNothing()
    {
        var a = await CreateTenantAsync("A");
        var email = $"{Guid.NewGuid():N}@test.local";
        var admin = factory.CreateClientFor(a.Admin);

        var response = await admin.PostJsonAsync("/api/users", NewUserRequest(UserRole.Client, null, email));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var list = await (await admin.GetAsync($"/api/users?search={email}")).ReadAsync<PagedResult<UserDto>>();
        Assert.Empty(list.Items);
    }

    [Fact]
    public async Task AgencyAdmin_ChangingAnEmployeeIntoClient_Returns403()
    {
        var a = await CreateTenantAsync("A");
        var seller = await factory.AddUserAsync(UserRole.Seller, a.Agency.Id);
        var request = new UpdateUserRequest("Test", "User", seller.Email, UserRole.Client, a.Agency.Id, true);

        var response = await factory.CreateClientFor(a.Admin).PutJsonAsync($"/api/users/{seller.Id}", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlatformAdmin_CreatesClient_WhoAppearsInTheAgencyListAndCanLogIn()
    {
        var a = await CreateTenantAsync("A");
        var platformAdmin = await factory.AddUserAsync(UserRole.PlatformAdmin, null);
        var email = $"{Guid.NewGuid():N}@test.local";
        var admin = factory.CreateClientFor(a.Admin);

        var created = await factory.CreateClientFor(platformAdmin)
            .PostJsonAsync("/api/users", NewUserRequest(UserRole.Client, a.Agency.Id, email));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var list = await (await admin.GetAsync($"/api/users?search={email}")).ReadAsync<Alhecasa.Application.Common.PagedResult<UserDto>>();
        var listed = Assert.Single(list.Items);
        Assert.Equal(UserRole.Client, listed.Role);
        Assert.Equal(a.Agency.Id, listed.AgencyId);

        var client = factory.CreateClient();
        var login = await client.PostJsonAsync("/api/auth/login", new Alhecasa.Application.Auth.LoginRequest(email, ApiFactory.UserPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.ReadAsync<Alhecasa.Application.Auth.LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Seller)]
    public async Task AgencyAdmin_CannotCreateEmployee_InAnotherAgency(UserRole role)
    {
        var a = await CreateTenantAsync("A");
        var b = await CreateTenantAsync("B");

        var response = await factory.CreateClientFor(a.Admin)
            .PostJsonAsync("/api/users", NewUserRequest(role, b.Agency.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlatformAdmin_CreatingEmployeeWithoutAgency_Returns400()
    {
        var platformAdmin = await factory.AddUserAsync(UserRole.PlatformAdmin, null);

        var response = await factory.CreateClientFor(platformAdmin)
            .PostJsonAsync("/api/users", NewUserRequest(UserRole.Seller, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Employees_AreIsolatedByAgency_LikeAnyOtherUser()
    {
        var a = await CreateTenantAsync("A");
        var b = await CreateTenantAsync("B");
        var sellerOfB = await factory.AddUserAsync(UserRole.Seller, b.Agency.Id);

        var response = await factory.CreateClientFor(a.Admin).GetAsync($"/api/users/{sellerOfB.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_CanFilterByEmployeeRole()
    {
        var a = await CreateTenantAsync("A");
        var seller = await factory.AddUserAsync(UserRole.Seller, a.Agency.Id);
        await factory.AddUserAsync(UserRole.Supervisor, a.Agency.Id);

        var page = await (await factory.CreateClientFor(a.Admin).GetAsync("/api/users?role=Seller"))
            .ReadAsync<PagedResult<UserDto>>();

        Assert.Equal([seller.Id], page.Items.Select(u => u.Id));
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_Returns409_EvenAcrossAgencies()
    {
        var a = await CreateTenantAsync("A");
        var b = await CreateTenantAsync("B");

        var response = await factory.CreateClientFor(a.Admin)
            .PostJsonAsync("/api/users", NewUserRequest(UserRole.Seller, null, b.Member.Email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidData_Returns400WithFieldErrors()
    {
        var platformAdmin = await factory.AddUserAsync(UserRole.PlatformAdmin, null);
        var request = new CreateUserRequest("", "Person", "not-an-email", "weak", UserRole.Client, null);

        var response = await factory.CreateClientFor(platformAdmin).PostJsonAsync("/api/users", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("FirstName", body, StringComparison.Ordinal);
        Assert.Contains("Email", body, StringComparison.Ordinal);
        Assert.Contains("Password", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_IsSoft_ThenUserIsGoneAndEmailCanBeReused()
    {
        var a = await CreateTenantAsync("A");
        var client = factory.CreateClientFor(a.Admin);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/users/{a.Member.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/users/{a.Member.Id}")).StatusCode);
        await factory.WithDbAsync(async db =>
        {
            var row = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == a.Member.Id);
            Assert.True(row.IsDeleted);
        });

        var recreated = await client.PostJsonAsync("/api/users", NewUserRequest(UserRole.Seller, null, a.Member.Email));
        Assert.Equal(HttpStatusCode.Created, recreated.StatusCode);
    }

    [Fact]
    public async Task List_Search_FiltersByTextAndPaginates()
    {
        var a = await CreateTenantAsync("A");
        var client = factory.CreateClientFor(a.Admin);
        var marker = Guid.NewGuid().ToString("N")[..8];
        await client.PostJsonAsync("/api/users", NewUserRequest(UserRole.Seller, null, $"{marker}-one@test.local"));
        await client.PostJsonAsync("/api/users", NewUserRequest(UserRole.Seller, null, $"{marker}-two@test.local"));

        var page = await (await client.GetAsync($"/api/users?search={marker}&pageSize=1"))
            .ReadAsync<PagedResult<UserDto>>();

        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal(2, page.TotalPages);
    }
}
