using Alhecasa.Application.Auth;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.UnitTests.Support;
using FluentValidation;

namespace Alhecasa.UnitTests.Application;

public sealed class RegistrationServiceTests
{
    private const string Password = "Correct-Horse-9";

    private readonly FakeUserRepository _users = new();
    private readonly FakeAgencyRepository _agencies = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private RegistrationService CreateService() => new(
        _users,
        _agencies,
        _unitOfWork,
        new FakePasswordHasher(),
        new FakeJwtTokenGenerator(),
        new RegisterRequestValidator(),
        TimeProvider.System);

    private static RegisterRequest NewRequest(string email = "ana@test.local", string password = Password) =>
        new("Ana", "Lopez", email, password);

    private async Task<IReadOnlyList<Agency>> AllAgenciesAsync() => await _agencies.ListAsync();

    [Fact]
    public async Task Register_CreatesAnIndividualSpace_AndItsAgencyAdmin_InOneSave()
    {
        var response = await CreateService().RegisterAsync(NewRequest());

        var agency = Assert.Single(await AllAgenciesAsync());
        var user = Assert.Single(_users.Users);
        Assert.Equal(AgencyKind.Individual, agency.Kind);
        Assert.Equal("Ana Lopez", agency.Name);
        Assert.Equal(UserRole.AgencyAdmin, user.Role);
        Assert.Equal(agency.Id, user.AgencyId);
        Assert.True(user.IsActive);
        Assert.NotNull(user.LastLoginAt);
        Assert.Equal($"hash:{Password}", user.PasswordHash);
        Assert.Equal(1, _unitOfWork.SaveCount);

        Assert.Equal($"token-for-{user.Id}", response.AccessToken);
        Assert.Equal(user.Id, response.User.Id);
        Assert.Equal(UserRole.AgencyAdmin, response.User.Role);
        Assert.Equal(agency.Id, response.User.AgencyId);
        Assert.Equal("Ana Lopez", response.User.AgencyName);
        Assert.True(response.User.IsIndividual);
    }

    [Fact]
    public async Task Register_NormalizesTheEmail_AndTrimsTheNames()
    {
        await CreateService().RegisterAsync(new("  Ana ", " Lopez  ", "  ANA@Test.Local ", Password));

        var user = Assert.Single(_users.Users);
        Assert.Equal("ana@test.local", user.Email);
        Assert.Equal("Ana", user.FirstName);
        Assert.Equal("Lopez", user.LastName);
        Assert.Equal("Ana Lopez", Assert.Single(await AllAgenciesAsync()).Name);
    }

    [Fact]
    public async Task Register_WithATakenEmail_IsAConflict_AndCreatesNothing()
    {
        _users.Users.Add(new User("Otra", "Persona", "ana@test.local", "hash:x", UserRole.Client, null));

        await Assert.ThrowsAsync<ConflictException>(() => CreateService().RegisterAsync(NewRequest("ANA@test.local")));

        Assert.Single(_users.Users);
        Assert.Empty(await AllAgenciesAsync());
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Register_WithAnEmailOfADeletedUser_IsAllowed()
    {
        var deleted = new User("Otra", "Persona", "ana@test.local", "hash:x", UserRole.Client, null);
        deleted.MarkAsDeleted(DateTimeOffset.UtcNow);
        _users.Users.Add(deleted);

        await CreateService().RegisterAsync(NewRequest());

        Assert.Equal(2, _users.Users.Count);
    }

    [Theory]
    [InlineData("", "Lopez", "ana@test.local", Password)]
    [InlineData("Ana", "", "ana@test.local", Password)]
    [InlineData("Ana", "Lopez", "no-es-correo", Password)]
    [InlineData("Ana", "Lopez", "ana@test.local", "corta1A")]
    [InlineData("Ana", "Lopez", "ana@test.local", "sinmayusculas123")]
    [InlineData("Ana", "Lopez", "ana@test.local", "SINMINUSCULAS123")]
    [InlineData("Ana", "Lopez", "ana@test.local", "SinNumeroAbcdef")]
    public async Task Register_WithInvalidData_FailsValidation_AndCreatesNothing(string first, string last, string email, string password)
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateService().RegisterAsync(new(first, last, email, password)));

        Assert.Empty(_users.Users);
        Assert.Empty(await AllAgenciesAsync());
    }

    [Fact]
    public async Task Register_WithTheLongestNames_StillFitsTheAgencyName()
    {
        var first = new string('A', 100);
        var last = new string('B', 100);

        await CreateService().RegisterAsync(new(first, last, "ana@test.local", Password));

        Assert.Equal(200, Assert.Single(await AllAgenciesAsync()).Name.Length);
    }
}