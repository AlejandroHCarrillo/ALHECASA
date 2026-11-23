using Alhecasa.Application.Auth;
using Alhecasa.Domain.Enums;
using Alhecasa.UnitTests.Support;

namespace Alhecasa.UnitTests.Application;

public sealed class SessionValidatorTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid AgencyId = Guid.CreateVersion7();

    private readonly FakeUserRepository _users = new();

    private SessionValidator CreateValidator() => new(_users);

    [Fact]
    public async Task ActiveAccountWithSameRoleAndAgency_IsValid()
    {
        _users.SessionStates[UserId] = new(UserRole.AgencyAdmin, AgencyId, IsActive: true, AgencyIsUsable: true);

        Assert.True(await CreateValidator().IsValidAsync(UserId, UserRole.AgencyAdmin, AgencyId));
    }

    [Fact]
    public async Task IndividualClientWithoutAgency_IsValid()
    {
        _users.SessionStates[UserId] = new(UserRole.Client, null, IsActive: true, AgencyIsUsable: true);

        Assert.True(await CreateValidator().IsValidAsync(UserId, UserRole.Client, null));
    }

    [Fact]
    public async Task UnknownOrDeletedUser_IsInvalid()
    {
        Assert.False(await CreateValidator().IsValidAsync(UserId, UserRole.Client, null));
    }

    [Fact]
    public async Task DeactivatedUser_IsInvalid()
    {
        _users.SessionStates[UserId] = new(UserRole.AgencyAdmin, AgencyId, IsActive: false, AgencyIsUsable: true);

        Assert.False(await CreateValidator().IsValidAsync(UserId, UserRole.AgencyAdmin, AgencyId));
    }

    [Fact]
    public async Task UserWhoseRoleChanged_IsInvalid()
    {
        _users.SessionStates[UserId] = new(UserRole.Client, AgencyId, IsActive: true, AgencyIsUsable: true);

        Assert.False(await CreateValidator().IsValidAsync(UserId, UserRole.AgencyAdmin, AgencyId));
    }

    [Fact]
    public async Task UserWhoseAgencyChanged_IsInvalid()
    {
        _users.SessionStates[UserId] = new(UserRole.AgencyAdmin, Guid.CreateVersion7(), IsActive: true, AgencyIsUsable: true);

        Assert.False(await CreateValidator().IsValidAsync(UserId, UserRole.AgencyAdmin, AgencyId));
    }

    [Fact]
    public async Task UserWhoseAgencyIsNoLongerUsable_IsInvalid()
    {
        _users.SessionStates[UserId] = new(UserRole.AgencyAdmin, AgencyId, IsActive: true, AgencyIsUsable: false);

        Assert.False(await CreateValidator().IsValidAsync(UserId, UserRole.AgencyAdmin, AgencyId));
    }
}
