using Alhecasa.Application.Inquiries;
using Alhecasa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alhecasa.Infrastructure.Persistence.Configurations;

internal sealed class InquiryConfiguration : IEntityTypeConfiguration<Inquiry>
{
    public void Configure(EntityTypeBuilder<Inquiry> builder)
    {
        builder.ToTable("Inquiries");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        // Los límites deben coincidir con InquiryLimits y CreateInquiryRequestValidator.
        builder.Property(i => i.PropertyTitle).IsRequired().HasMaxLength(150);
        builder.Property(i => i.Name).IsRequired().HasMaxLength(InquiryLimits.Name);
        builder.Property(i => i.Phone).IsRequired().HasMaxLength(InquiryLimits.Phone);
        builder.Property(i => i.Email).IsRequired().HasMaxLength(InquiryLimits.Email);
        builder.Property(i => i.Message).IsRequired().HasMaxLength(InquiryLimits.Text);
        builder.Property(i => i.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(i => i.CreatedAt).IsRequired();

        // Sin navegación a la propiedad: la solicitud guarda el título y sigue existiendo aunque la propiedad se elimine (borrado lógico).
        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(i => i.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Agency>()
            .WithMany()
            .HasForeignKey(i => i.AgencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.FollowUps)
            .WithOne()
            .HasForeignKey(f => f.InquiryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.FollowUps).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(i => new { i.AgencyId, i.Status, i.CreatedAt });
        builder.HasIndex(i => i.PropertyId);
    }
}

internal sealed class InquiryFollowUpConfiguration : IEntityTypeConfiguration<InquiryFollowUp>
{
    public void Configure(EntityTypeBuilder<InquiryFollowUp> builder)
    {
        builder.ToTable("InquiryFollowUps");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Kind).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(f => f.Text).IsRequired().HasMaxLength(InquiryLimits.Text);
        builder.Property(f => f.AuthorName).IsRequired().HasMaxLength(201);
        builder.Property(f => f.CreatedAt).IsRequired();

        builder.HasIndex(f => f.InquiryId);
        builder.HasIndex(f => f.AgencyId);
    }
}