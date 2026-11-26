using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;

namespace Alhecasa.UnitTests.Domain;

public sealed class AgencyTests
{
    [Fact]
    public void ARegularAgency_IsACompany()
    {
        var agency = new Agency("Inmobiliaria", "a@a.com", null);

        Assert.Equal(AgencyKind.Company, agency.Kind);
        Assert.False(agency.IsIndividual);
    }

    [Fact]
    public void AFreeAccount_IsAnIndividualWithNoPhone()
    {
        var agency = Agency.CreateIndividual("Ana Lopez", "ana@a.com");

        Assert.Equal(AgencyKind.Individual, agency.Kind);
        Assert.True(agency.IsIndividual);
        Assert.Equal("Ana Lopez", agency.Name);
        Assert.Equal("ana@a.com", agency.Email);
        Assert.Null(agency.Phone);
        Assert.True(agency.IsActive);
    }
}