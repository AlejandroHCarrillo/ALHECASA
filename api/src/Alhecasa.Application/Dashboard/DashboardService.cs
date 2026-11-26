using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Application.Dashboard;

/// <summary>Resumen de actividad de la inmobiliaria del usuario.</summary>
public interface IDashboardService
{
    /// <summary>Solo del personal de una inmobiliaria (o del particular en su espacio); si no, <see cref="ForbiddenException"/>.</summary>
    Task<AgencyDashboardDto> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class DashboardService(
    IPropertyRepository properties,
    IInquiryRepository inquiries,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IDashboardService
{
    /// <summary>Ventana de «solicitudes recibidas recientemente».</summary>
    public const int RecentDays = 7;

    /// <summary>Cuántas solicitudes nuevas se listan en el tablero.</summary>
    public const int LatestInquiriesCount = 5;

    public async Task<AgencyDashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.Role is not (UserRole.AgencyAdmin or UserRole.Supervisor or UserRole.Seller)
            || currentUser.AgencyId is not { } agencyId)
        {
            throw new ForbiddenException("Only agency staff can see the dashboard.");
        }

        var now = timeProvider.GetUtcNow();

        // Las consultas comparten el DbContext de la solicitud: se hacen una tras otra, no en paralelo.
        var propertyStats = await properties.GetStatsAsync(agencyId, now, cancellationToken);
        var inquiryStats = await inquiries.GetStatsAsync(agencyId, now.AddDays(-RecentDays), cancellationToken);
        var latest = await inquiries.ListLatestNewAsync(agencyId, LatestInquiriesCount, cancellationToken);

        return new AgencyDashboardDto(
            propertyStats,
            inquiryStats,
            RecentDays,
            latest.Select(i => new RecentInquiryDto(i.Id, i.PropertyId, i.PropertyTitle, i.Name, i.CreatedAt)).ToList());
    }
}