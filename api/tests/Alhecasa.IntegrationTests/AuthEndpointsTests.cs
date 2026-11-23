using System.Net;
using Alhecasa.Application.Auth;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;

namespace Alhecasa.IntegrationTests;

// Máximo 10 inicios de sesión por minuto desde una misma IP; esta clase usa pocos para no tocar el límite.
public sealed class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenThatWorksOnProtectedEndpoints()
    {
        var user = await factory.AddUserAsync(UserRole.Client, null);
        var client = factory.CreateClient();

        var response = await client.PostJsonAsync("/api/auth/login", new LoginRequest(user.Email, ApiFactory.UserPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.ReadAsync<LoginResponse>();
        Assert.NotEmpty(login.AccessToken);
        Assert.Equal(user.Id, login.User.Id);

        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(user.Id, (await me.ReadAsync<CurrentUserDto>()).Id);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401ProblemDetails()
    {
        var user = await factory.AddUserAsync(UserRole.Client, null);

        var response = await factory.CreateClient()
            .PostJsonAsync("/api/auth/login", new LoginRequest(user.Email, "Wrong-Password-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadProblemAsync();
        Assert.Equal(401, problem.Status);
        Assert.Equal("Invalid email or password.", problem.Detail);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_GivesSameAnswerAsWrongPassword()
    {
        var response = await factory.CreateClient()
            .PostJsonAsync("/api/auth/login", new LoginRequest("nobody@test.local", "Wrong-Password-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Invalid email or password.", (await response.ReadProblemAsync()).Detail);
    }

    [Fact]
    public async Task Login_WithDeactivatedUser_Returns401()
    {
        var user = await factory.AddUserAsync(UserRole.Client, null);
        await factory.ModifyUserAsync(user.Id, u => u.Update(u.FirstName, u.LastName, u.Email, u.Role, u.AgencyId, isActive: false));

        var response = await factory.CreateClient()
            .PostJsonAsync("/api/auth/login", new LoginRequest(user.Email, ApiFactory.UserPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmptyBody_Returns400ProblemDetailsWithErrors()
    {
        var response = await factory.CreateClient()
            .PostJsonAsync("/api/auth/login", new LoginRequest("", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/api/users")]
    [InlineData("/api/agencies")]
    public async Task ProtectedEndpoints_WithoutToken_Return401(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithGarbageToken_Return401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not.a.jwt");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task HealthCheck_IsAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Responses_IncludeSecurityHeaders()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }
}

public sealed class LoginRateLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Login_AfterTenAttemptsInAMinute_Returns429()
    {
        var client = factory.CreateClient();
        var request = new LoginRequest("nobody@test.local", "Wrong-Password-1");

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostJsonAsync("/api/auth/login", request)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostJsonAsync("/api/auth/login", request)).StatusCode);
    }
}
