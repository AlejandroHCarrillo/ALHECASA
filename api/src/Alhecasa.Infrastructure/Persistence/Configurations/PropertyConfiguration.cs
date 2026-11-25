using Alhecasa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alhecasa.Infrastructure.Persistence.Configurations;

internal sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Title).IsRequired().HasMaxLength(150);
        builder.Property(p => p.Description).HasMaxLength(2000);

        // Los enums se guardan como texto: la base es legible y estable ante cambios del enum.
        builder.Property(p => p.Type).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.Operation).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.Currency).IsRequired().HasConversion<string>().HasMaxLength(3);

        // Los límites de precio y superficie deben coincidir con PropertyRequestValidator.
        builder.Property(p => p.Price).IsRequired().HasPrecision(15, 2);
        builder.Property(p => p.ConstructionArea).HasPrecision(10, 2);
        builder.Property(p => p.LandArea).HasPrecision(10, 2);

        // Grados con 6 decimales (unos 10 cm). Los rangos los valida el dominio y PropertyRequestValidator.
        builder.Property(p => p.Latitude).HasPrecision(9, 6);
        builder.Property(p => p.Longitude).HasPrecision(9, 6);

        builder.Property(p => p.Street).HasMaxLength(200);
        builder.Property(p => p.Neighborhood).HasMaxLength(100);
        builder.Property(p => p.City).IsRequired().HasMaxLength(100);
        builder.Property(p => p.State).IsRequired().HasMaxLength(100);
        builder.Property(p => p.PostalCode).HasMaxLength(10);

        builder.Property(p => p.CreatedAt).IsRequired();

        builder.HasOne(p => p.Agency)
            .WithMany()
            .HasForeignKey(p => p.AgencyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Las imágenes se quitan desde la propiedad (se borran al sacarlas de la colección). No se exponen como colección editable.
        builder.HasMany(p => p.Images)
            .WithOne()
            .HasForeignKey(i => i.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Images).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => p.AgencyId);
    }
}
