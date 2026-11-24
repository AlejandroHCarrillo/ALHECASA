using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;

namespace Alhecasa.UnitTests.Domain;

public sealed class UserTests
{
    private static readonly Guid AgencyId = Guid.CreateVersion7();

    private static User NewUser(UserRole role, Guid? agencyId) =>
        new("Ana", "Lopez", "ana@test.local", "hash", role, agencyId);

    [Fact]
    public void PlatformAdmin_WithAgency_IsRejected()
    {
        Assert.Throws<DomainException>(() => NewUser(UserRole.PlatformAdmin, AgencyId));
    }

    [Fact]
    public void AgencyAdmin_WithoutAgency_IsRejected()
    {
        Assert.Throws<DomainException>(() => NewUser(UserRole.AgencyAdmin, null));
    }

    [Theory]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Seller)]
    public void AgencyStaff_WithoutAgency_IsRejected(UserRole role)
    {
        Assert.Throws<DomainException>(() => NewUser(role, null));
    }

    [Theory]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Seller)]
    public void AgencyStaff_WithAgency_IsAccepted(UserRole role)
    {
        var user = NewUser(role, AgencyId);

        Assert.Equal(role, user.Role);
        Assert.Equal(AgencyId, user.AgencyId);
    }

    [Fact]
    public void PlatformAdmin_WithoutAgency_IsAccepted()
    {
        var user = NewUser(UserRole.PlatformAdmin, null);

        Assert.Equal(UserRole.PlatformAdmin, user.Role);
        Assert.Null(user.AgencyId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Client_CanBeIndividualOrBelongToAgency(bool belongsToAgency)
    {
        var user = NewUser(UserRole.Client, belongsToAgency ? AgencyId : null);

        Assert.Equal(UserRole.Client, user.Role);
        Assert.Equal(belongsToAgency ? AgencyId : null, user.AgencyId);
    }

    [Fact]
    public void Update_ToInvalidRoleAgencyCombination_IsRejectedAndKeepsRole()
    {
        var user = NewUser(UserRole.AgencyAdmin, AgencyId);

        Assert.Throws<DomainException>(() =>
            user.Update("Ana", "Lopez", "ana@test.local", UserRole.AgencyAdmin, null, isActive: true));

        Assert.Equal(UserRole.AgencyAdmin, user.Role);
        Assert.Equal(AgencyId, user.AgencyId);
    }

    [Fact]
    public void NewUser_IsActive_AndHasVersion7Id()
    {
        var user = NewUser(UserRole.Client, null);

        Assert.True(user.IsActive);
        Assert.Equal(7, user.Id.Version);
    }

    [Fact]
    public void FullName_JoinsFirstAndLastName()
    {
        Assert.Equal("Ana Lopez", NewUser(UserRole.Client, null).FullName);
    }

    [Fact]
    public void MarkAsDeleted_SetsSoftDeleteFlagAndDate()
    {
        var user = NewUser(UserRole.Client, null);
        var now = DateTimeOffset.UtcNow;

        user.MarkAsDeleted(now);

        Assert.True(user.IsDeleted);
        Assert.Equal(now, user.DeletedAt);
    }
}
