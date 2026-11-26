using Alhecasa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alhecasa.Infrastructure.Persistence.Configurations;

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Plans");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        // Los límites deben coincidir con PlanRequestValidator.
        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.Kind).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Audience).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Period).HasConversion<string>().HasMaxLength(10);
        builder.Property(p => p.Currency).IsRequired().HasConversion<string>().HasMaxLength(3);
        builder.Property(p => p.Price).IsRequired().HasPrecision(15, 2);
        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();

        // IsFree se deduce del precio: no se guarda.
        builder.Ignore(p => p.IsFree);
    }
}

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.PlanName).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Kind).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Period).IsRequired().HasConversion<string>().HasMaxLength(10);
        builder.Property(s => s.Currency).IsRequired().HasConversion<string>().HasMaxLength(3);
        builder.Property(s => s.AmountPaid).IsRequired().HasPrecision(15, 2);
        builder.Property(s => s.PaymentReference).HasMaxLength(200);
        builder.Property(s => s.StartsAt).IsRequired();
        builder.Property(s => s.EndsAt).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasOne<Agency>()
            .WithMany()
            .HasForeignKey(s => s.AgencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Plan>()
            .WithMany()
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.AgencyId, s.Kind });
    }
}
