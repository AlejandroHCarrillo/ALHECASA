using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Application.Users;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.UnitTests.Support;
using FluentValidation;

namespace Alhecasa.UnitTests.Application;

public sealed class UserServiceTests
{
    private const string ValidPassword = "Correct-Horse-9";

    private readonly FakeUserRepository _users = new();
    private readonly FakeAgencyRepository _agencies = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Agency _agencyA;
    private readonly Agency _agencyB;

    public UserServiceTests()
    {
        _agencyA = _agencies.Add("Agency A");
        _agencyB = _agencies.Add("Agency B");
    }

    private UserService CreateService(ICurrentUser currentUser) => new(
        _users,
        _agencies,
        _unitOfWork,
        new FakePasswordHasher(),
        currentUser,
        new CreateUserRequestValidator(),
        new UpdateUserRequestValidator(),
        TimeProvider.System);

    private User AddUser(UserRole role, Guid? agencyId, string? email = null)
    {
        var user = new User("Test", "User", email ?? $"{Guid.NewGuid():N}@test.local", "hash:x", role, agencyId);
        _users.Users.Add(user);
        return user;
    }

    private static CreateUserRequest NewRequest(UserRole role, Guid? agencyId, string email = "new@test.local") =>
        new("New", "Person", email, ValidPassword, role, agencyId);

    // --- Quién puede administrar usuarios ---------------------------------------------------

    [Fact]
    public async Task Client_CannotListUsers()
    {
        var service = CreateService(FakeCurrentUser.Client());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetPagedAsync(new UserListQuery()));
    }

    [Fact]
    public async Task Client_CannotCreateUsers()
    {
        var service = CreateService(FakeCurrentUser.Client());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(NewRequest(UserRole.Client, null)));
    }

    // --- Alta -------------------------------------------------------------------------------

    [Fact]
    public async Task AgencyAdmin_CreatesUser_InOwnAgency_EvenWithoutSendingAgencyId()
    {
        var service = CreateService(FakeCurrentUser.AgencyAdmin(_agencyA.Id));

        var created = await service.CreateAsync(NewRequest(UserRole.Client, null));

        Assert.Equal(_agencyA.Id, created.AgencyId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AgencyAdmin_CannotCreateUser_InAnotherAgency()
    {
        var service = CreateService(FakeCurrentUser.AgencyAdmin(_agencyA.Id));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateAsync(NewRequest(UserRole.Client, _agencyB.Id)));
    }

    [Fact]
    public async Task AgencyAdmin_CannotCreatePlatformAdmin()
    {
        var service = CreateService(FakeCurrentUser.AgencyAdmin(_agencyA.Id));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateAsync(NewRequest(UserRole.PlatformAdmin, null)));
    }

    [Fact]
    public async Task PlatformAdmin_CreatesPlatformAdmin_WithoutAgencyEvenIfOneIsSent()
    {
        var service = CreateService(FakeCurrentUser.PlatformAdmin());

        var created = await service.CreateAsync(NewRequest(UserRole.PlatformAdmin, _agencyA.Id));

        Assert.Equal(UserRole.PlatformAdmin, created.Role);
        Assert.Null(created.AgencyId);
    }

    [Fact]
    public async Task PlatformAdmin_CreatingAgencyAdminForUnknownAgency_IsNotFound()
    {
        var service = CreateService(FakeCurrentUser.PlatformAdmin());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateAsync(NewRequest(UserRole.AgencyAdmin, Guid.CreateVersion7())));
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_IsConflict_AndIgnoresCase()
    {
        AddUser(UserRole.Client, null, email: "dup@test.local");
        var service = CreateService(FakeCurrentUser.PlatformAdmin());

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(NewRequest(UserRole.Client, null, email: "  DUP@Test.Local ")));
    }

    [Fact]
    public async Task Create_StoresHashedPassword_NotThePlainOne()
    {
        var service = CreateService(FakeCurrentUser.PlatformAdmin());

        await service.CreateAsync(NewRequest(UserRole.Client, null));

        var stored = Assert.Single(_users.Users);
        Assert.NotEqual(ValidPassword, stored.PasswordHash);
        Assert.StartsWith("hash:", stored.PasswordHash, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("short1A")]
    [InlineData("alllowercase123")]
    [InlineData("ALLUPPERCASE123")]
    [InlineData("NoDigitsHereAtAll")]
    public async Task Create_WithWeakPassword_FailsValidation(string password)
    {
        var service = CreateService(FakeCurrentUser.PlatformAdmin());
        var request = NewRequest(UserRole.Client, null) with { Password = password };

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request));
        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task Create_WithInvalidEmail_FailsValidation()
    {
        var service = CreateService(FakeCurrentUser.PlatformAdmin());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(NewRequest(UserRole.Client, null, email: "not-an-email")));
    }

    // --- Consulta ---------------------------------------------------------------------------

    [Fact]
    public async Task GetById_OfMissingUser_IsNotFound()
    {
        var service = CreateService(FakeCurrentUser.PlatformAdmin());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(Guid.CreateVersion7()));
    }

    // --- Modificación -----------------------------------------------------------------------

    [Fact]
    public async Task Update_OwnRole_IsForbidden()
    {
        var self = AddUser(UserRole.AgencyAdmin, _agencyA.Id);
        var service = CreateService(FakeCurrentUser.AgencyAdmin(_agencyA.Id, self.Id));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(
            self.Id, new("Test", "User", self.Email, UserRole.Client, _agencyA.Id, true)));
    }

    [Fact]
    public async Task Update_DeactivatingYourself_IsForbidden()
    {
        var self = AddUser(UserRole.AgencyAdmin, _agencyA.Id);
        var service = CreateService(FakeCurrentUser.AgencyAdmin(_agencyA.Id, self.Id));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(
            self.Id, new("Test", "User", self.Email, UserRole.AgencyAdmin, _agencyA.Id, false)));
    }

    [Fact]
    public async Task AgencyAdmin_CannotModifyPlatformAdmin()
    {
        var platformAdmin = AddUser(UserRole.PlatformAdmin, null);
        var service = CreateService(FakeCurrentUser.AgencyAdmin(_agencyA.Id));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(
            platformAdmin.Id, new("X", "Y", platformAdmin.Email, UserRole.PlatformAdmin, null, true)));
    }

    [Fact]
    public async Task Update_ToEmailOfAnotherUser_IsConflict()
    {
        var first = AddUser(UserRole.Client, null, "first@test.local");
        AddUser(UserRole.Client, null, "second@test.local");
        var service = CreateService(FakeCurrentUser.PlatformAdmin());

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(
            first.Id, new("Test", "User", "second@test.local", UserRole.Client, null, true)));
    }

    [Fact]
    public async Task Update_KeepingOwnEmail_Succeeds()
    {
        var user = AddUser(UserRole.Client, null, "same@test.local");
        var service = CreateService(FakeCurrentUser.PlatformAdmin());

        var updated = await service.UpdateAsync(
            user.Id, new("Renamed", "User", "same@test.local", UserRole.Client, null, true));

        Assert.Equal("Renamed", updated.FirstName);
    }

    [Fact]
    public async Task Update_OfMissingUser_IsNotFound()
    {
        var service = CreateService(FakeCurrentUser.PlatformAdmin());

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(
            Guid.CreateVersion7(), new("A", "B", "a@test.local", UserRole.Client, null, true)));
    }

    // --- Baja -------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Yourself_IsForbidden()
    {
        var self = AddUser(UserRole.AgencyAdmin, _agencyA.Id);
        var service = CreateService(FakeCurrentUser.AgencyAdmin(_agencyA.Id, self.Id));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.DeleteAsync(self.Id));
    }

    [Fact]
    public async Task AgencyAdmin_CannotDeletePlatformAdmin()
    {
        var platformAdmin = AddUser(UserRole.PlatformAdmin, null);
        var service = CreateService(FakeCurrentUser.AgencyAdmin(_agencyA.Id));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.DeleteAsync(platformAdmin.Id));
        Assert.False(platformAdmin.IsDeleted);
    }

    [Fact]
    public async Task Delete_MarksUserAsSoftDeleted()
    {
        var user = AddUser(UserRole.Client, _agencyA.Id);
        var service = CreateService(FakeCurrentUser.PlatformAdmin());

        await service.DeleteAsync(user.Id);

        Assert.True(user.IsDeleted);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }
}
