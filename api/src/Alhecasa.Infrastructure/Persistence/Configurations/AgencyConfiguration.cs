using Alhecasa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alhecasa.Infrastructure.Persistence.Configurations;

internal sealed class AgencyConfiguration : IEntityTypeConfiguration<Agency>
{
    public void Configure(EntityTypeBuilder<Agency> builder)
    {
        builder.ToTable("Agencies");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Name).IsRequired().HasMaxLength(AgencyProfileLimits.Name);
        builder.Property(a => a.Email).IsRequired().HasMaxLength(AgencyProfileLimits.Email);
        builder.Property(a => a.Phone).HasMaxLength(AgencyProfileLimits.Phone);
        builder.Property(a => a.Description).HasMaxLength(AgencyProfileLimits.Description);
        builder.Property(a => a.Website).HasMaxLength(AgencyProfileLimits.Url);
        builder.Property(a => a.FacebookUrl).HasMaxLength(AgencyProfileLimits.Url);
        builder.Property(a => a.InstagramUrl).HasMaxLength(AgencyProfileLimits.Url);
        builder.Property(a => a.OpeningHours).HasMaxLength(AgencyProfileLimits.OpeningHours);
        builder.Property(a => a.Street).HasMaxLength(AgencyProfileLimits.Street);
        builder.Property(a => a.City).HasMaxLength(AgencyProfileLimits.City);
        builder.Property(a => a.State).HasMaxLength(AgencyProfileLimits.State);
        builder.Property(a => a.PostalCode).HasMaxLength(AgencyProfileLimits.PostalCode);
        builder.Property(a => a.LogoStorageKey).HasMaxLength(64);
        builder.Property(a => a.LogoContentType).HasMaxLength(50);
        builder.Ignore(a => a.HasLogo);        builder.Property(a => a.Kind).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.IsActive).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
    }
}
