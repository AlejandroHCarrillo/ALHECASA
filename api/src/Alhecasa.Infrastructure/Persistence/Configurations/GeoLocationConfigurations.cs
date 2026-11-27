using Alhecasa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alhecasa.Infrastructure.Persistence.Configurations;

internal sealed class GeoStateConfiguration : IEntityTypeConfiguration<GeoState>
{
    public void Configure(EntityTypeBuilder<GeoState> builder)
    {
        builder.ToTable("GeoStates");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).IsRequired().HasMaxLength(GeoLimits.Name);
        builder.Property(s => s.Slug).IsRequired().HasMaxLength(GeoLimits.Name);
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasIndex(s => s.Slug).IsUnique();
    }
}

internal sealed class GeoCityConfiguration : IEntityTypeConfiguration<GeoCity>
{
    public void Configure(EntityTypeBuilder<GeoCity> builder)
    {
        builder.ToTable("GeoCities");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(GeoLimits.Name);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(GeoLimits.Slug);
        builder.Property(c => c.CreatedAt).IsRequired();

        builder.HasOne(c => c.State)
            .WithMany()
            .HasForeignKey(c => c.StateId)
            .OnDelete(DeleteBehavior.Restrict);

        // El identificador forma la dirección pública de la zona: es único en toda la plataforma.
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.HasIndex(c => c.StateId);
    }
}

internal sealed class GeoNeighborhoodConfiguration : IEntityTypeConfiguration<GeoNeighborhood>
{
    public void Configure(EntityTypeBuilder<GeoNeighborhood> builder)
    {
        builder.ToTable("GeoNeighborhoods");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Name).IsRequired().HasMaxLength(GeoLimits.Name);
        builder.Property(n => n.Slug).IsRequired().HasMaxLength(GeoLimits.Name);
        builder.Property(n => n.CreatedAt).IsRequired();

        builder.HasOne(n => n.City)
            .WithMany()
            .HasForeignKey(n => n.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.CityId, n.Slug }).IsUnique();
    }
}
