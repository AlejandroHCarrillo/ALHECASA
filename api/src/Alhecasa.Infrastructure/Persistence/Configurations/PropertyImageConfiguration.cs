using Alhecasa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alhecasa.Infrastructure.Persistence.Configurations;

internal sealed class PropertyImageConfiguration : IEntityTypeConfiguration<PropertyImage>
{
    public void Configure(EntityTypeBuilder<PropertyImage> builder)
    {
        builder.ToTable("PropertyImages");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.StorageKey).IsRequired().HasMaxLength(100);
        builder.Property(i => i.ContentType).IsRequired().HasMaxLength(50);
        builder.Property(i => i.SizeBytes).IsRequired();
        builder.Property(i => i.SortOrder).IsRequired();
        builder.Property(i => i.CreatedAt).IsRequired();

        builder.HasIndex(i => i.PropertyId);
        builder.HasIndex(i => i.AgencyId);
    }
}
