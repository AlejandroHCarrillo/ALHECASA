using Alhecasa.Application.Auth;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.UnitTests.Support;
using FluentValidation;

namespace Alhecasa.UnitTests.Application;

public sealed class AuthServiceTests
{
    private const string Password = "Correct-Horse-9";

    private readonly FakeUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private AuthService CreateService(FakeCurrentUser? currentUser = null) => new(
        _users,
        _unitOfWork,
        _hasher,
        new FakeJwtTokenGenerator(),
        currentUser ?? new FakeCurrentUser(),
        new LoginRequestValidator(),
        TimeProvider.System);

    private User AddUser(string email = "ana@test.local", UserRole role = UserRole.Client)
    {
        var user = new User("Ana", "Lopez", email, _hasher.Hash(Password), role, null);
        _users.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndRegistersLogin()
    {
        var user = AddUser();

        var response = await CreateService().LoginAsync(new("ana@test.local", Password));

        Assert.Equal($"token-for-{user.Id}", response.AccessToken);
        Assert.Equal(user.Id, response.User.Id);
        Assert.NotNull(user.LastLoginAt);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Login_NormalizesEmail()
    {
        AddUser("ana@test.local");

        var response = await CreateService().LoginAsync(new("  ANA@Test.Local ", Password));

        Assert.Equal("ana@test.local", response.User.Email);
    }

    [Fact]
    public async Task Login_WithWrongPassword_FailsWithGenericMessage()
    {
        AddUser();

        var wrongPassword = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            CreateService().LoginAsync(new("ana@test.local", "Wrong-Password-1")));
        var unknownUser = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            CreateService().LoginAsync(new("nobody@test.local", Password)));

        // El mensaje es idéntico: no se revela qué correos están registrados.
        Assert.Equal(wrongPassword.Message, unknownUser.Message);
    }

    [Fact]
    public async Task Login_WithUnknownUser_StillRunsDummyVerification()
    {
        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            CreateService().LoginAsync(new("nobody@test.local", Password)));

        Assert.Equal(1, _hasher.DummyVerifications);
    }

    [Fact]
    public async Task Login_WithDeactivatedUser_Fails()
    {
        var user = AddUser();
        user.Update("Ana", "Lopez", user.Email, user.Role, user.AgencyId, isActive: false);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            CreateService().LoginAsync(new("ana@test.local", Password)));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Login_WithEmptyFields_FailsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().LoginAsync(new("", "")));
    }

    [Fact]
    public async Task GetCurrentUser_WithoutAuthentication_Fails()
    {
        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            CreateService().GetCurrentUserAsync());
    }

    [Fact]
    public async Task GetCurrentUser_OfActiveUser_ReturnsProfile()
    {
        var user = AddUser();

        var profile = await CreateService(FakeCurrentUser.Client(user.Id)).GetCurrentUserAsync();

        Assert.Equal(user.Id, profile.Id);
    }
}
